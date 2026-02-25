using System.Data.SqlClient;
using Dapper;
using Sh.Autofit.OrderBoard.Web.Models;
using Sh.Autofit.OrderBoard.Web.Models.Dtos;

namespace Sh.Autofit.OrderBoard.Web.Services;

public interface IArchiveService
{
    Task<ArchiveBoardResponse> BuildArchiveBoardAsync(DateTime date);
    Task<List<OrderTimelineEvent>> GetOrderTimelineAsync(int appOrderId);
}

public class ArchiveService : IArchiveService
{
    private readonly string _connectionString;

    private static readonly (string Stage, string Label)[] Stages =
    [
        ("ORDER_IN_PC", "הזמנה במחשב"),
        ("ORDER_PRINTED", "הזמנה הודפסה"),
        ("DOC_IN_PC", "מסמך במחשב"),
        ("PACKING", "אריזה"),
        ("PACKED", "נארז"),
    ];

    public ArchiveService(string connectionString)
    {
        _connectionString = connectionString;
    }

    private SqlConnection CreateConnection() => new(_connectionString);

    public async Task<ArchiveBoardResponse> BuildArchiveBoardAsync(DateTime date)
    {
        var startOfDay = date.Date;
        var endOfDay = date.Date.AddDays(1).AddMilliseconds(-3);

        // Single CTE query to reconstruct board state at end-of-day
        const string sql = @"
            ;WITH StageAtDate AS (
                SELECT se.AppOrderId, se.ToStage, se.At,
                       ROW_NUMBER() OVER (PARTITION BY se.AppOrderId ORDER BY se.At DESC, se.EventId DESC) AS rn
                FROM dbo.StageEvents se
                WHERE se.Action = 'MOVE_STAGE'
                  AND se.At <= @EndOfDay
                  AND se.AppOrderId IS NOT NULL
            ),
            HideStatus AS (
                SELECT se.AppOrderId,
                       SUM(CASE WHEN se.Action IN ('HIDE', 'BULK_HIDE') THEN 1 ELSE 0 END) AS HideCount,
                       SUM(CASE WHEN se.Action = 'UNHIDE' THEN 1 ELSE 0 END) AS UnhideCount
                FROM dbo.StageEvents se
                WHERE se.Action IN ('HIDE', 'BULK_HIDE', 'UNHIDE')
                  AND se.At <= @EndOfDay
                  AND se.AppOrderId IS NOT NULL
                GROUP BY se.AppOrderId
            ),
            PackedAt AS (
                SELECT se.AppOrderId,
                       MIN(se.At) AS FirstPackedAt
                FROM dbo.StageEvents se
                WHERE se.Action = 'MOVE_STAGE' AND se.ToStage = 'PACKED'
                  AND se.At <= @EndOfDay
                  AND se.AppOrderId IS NOT NULL
                GROUP BY se.AppOrderId
            )
            SELECT o.AppOrderId, o.AccountKey, o.AccountName, o.City, o.Address, o.Phone,
                   o.DisplayTime, o.IsManual, o.ManualNote, o.CreatedAt,
                   o.DeliveryMethodId, o.DeliveryRunId, o.Pinned, o.NeedsResolve,
                   COALESCE(s.ToStage, o.CurrentStage) AS StageAtDate,
                   COALESCE(s.At, o.StageUpdatedAt) AS StageUpdatedAtDate,
                   p.FirstPackedAt AS PackedAt,
                   CASE WHEN ISNULL(h.HideCount, 0) > ISNULL(h.UnhideCount, 0) THEN 1 ELSE 0 END AS WasHidden
            FROM dbo.AppOrders o
            LEFT JOIN StageAtDate s ON s.AppOrderId = o.AppOrderId AND s.rn = 1
            LEFT JOIN HideStatus h ON h.AppOrderId = o.AppOrderId
            LEFT JOIN PackedAt p ON p.AppOrderId = o.AppOrderId
            WHERE o.CreatedAt <= @EndOfDay
              AND (o.MergedIntoAppOrderId IS NULL OR o.UpdatedAt > @EndOfDay)
            ORDER BY o.DisplayTime DESC, o.AppOrderId DESC";

        using var conn = CreateConnection();
        await conn.OpenAsync();

        var rows = (await conn.QueryAsync<ArchiveOrderRow>(sql,
            new { EndOfDay = endOfDay }, commandTimeout: 30)).ToList();

        // Fetch delivery methods that were active at that date
        const string methodsSql = @"
            SELECT * FROM dbo.DeliveryMethods
            WHERE CreatedAt <= @EndOfDay
              AND (ClosedAt IS NULL OR ClosedAt > @EndOfDay)";

        var methods = (await conn.QueryAsync<DeliveryMethod>(methodsSql,
            new { EndOfDay = endOfDay })).ToList();

        var methodLookup = methods.ToDictionary(m => m.DeliveryMethodId);

        // Separate visible vs hidden
        var visibleRows = rows.Where(r => !r.WasHidden).ToList();
        var hiddenRows = rows.Where(r => r.WasHidden).ToList();

        // Build columns from visible orders
        var columns = BuildColumns(visibleRows, methodLookup);

        // Build summary
        var summary = new ArchiveDaySummary
        {
            TotalOrders = visibleRows.Count,
            OrdersPacked = visibleRows.Count(r => r.StageAtDate == "PACKED"),
            OrdersCreated = visibleRows.Count(r => r.CreatedAt >= startOfDay && r.CreatedAt <= endOfDay),
            ByStage = visibleRows.GroupBy(r => r.StageAtDate)
                .ToDictionary(g => g.Key, g => g.Count())
        };

        // Build delivery method DTOs
        var methodDtos = methods.Select(m => new DeliveryMethodDto
        {
            DeliveryMethodId = m.DeliveryMethodId,
            Name = m.Name,
            IsAdHoc = m.IsAdHoc,
            IsActive = m.IsActive,
            WindowStartTime = m.WindowStartTime?.ToString(@"hh\:mm"),
            WindowEndTime = m.WindowEndTime?.ToString(@"hh\:mm"),
            Runs = []
        }).ToList();

        return new ArchiveBoardResponse
        {
            Date = date.Date,
            Columns = columns,
            DeliveryMethods = methodDtos,
            Summary = summary
        };
    }

    public async Task<List<OrderTimelineEvent>> GetOrderTimelineAsync(int appOrderId)
    {
        const string sql = @"
            SELECT EventId, At, Actor, Action, FromStage, ToStage
            FROM dbo.StageEvents
            WHERE AppOrderId = @AppOrderId
            ORDER BY At ASC, EventId ASC";

        using var conn = CreateConnection();
        await conn.OpenAsync();
        return (await conn.QueryAsync<OrderTimelineEvent>(sql,
            new { AppOrderId = appOrderId })).ToList();
    }

    private List<BoardColumn> BuildColumns(
        List<ArchiveOrderRow> rows,
        Dictionary<int, DeliveryMethod> methodLookup)
    {
        var columns = new List<BoardColumn>();

        foreach (var (stage, label) in Stages)
        {
            var stageRows = rows.Where(r => r.StageAtDate == stage).ToList();

            List<DeliveryGroupDto> groups;
            if (stage is "PACKING" or "PACKED")
            {
                groups = BuildDeliveryGroups(stageRows, methodLookup);
            }
            else
            {
                var cards = stageRows
                    .OrderByDescending(r => r.DisplayTime ?? DateTime.MinValue)
                    .Select(r => MapToCard(r, methodLookup))
                    .ToList();
                cards = StackCards(cards);
                groups =
                [
                    new DeliveryGroupDto
                    {
                        Title = "",
                        Orders = cards,
                        Count = stageRows.Count
                    }
                ];
            }

            columns.Add(new BoardColumn
            {
                Stage = stage,
                Label = label,
                Count = stageRows.Count,
                Groups = groups
            });
        }

        return columns;
    }

    private List<DeliveryGroupDto> BuildDeliveryGroups(
        List<ArchiveOrderRow> rows,
        Dictionary<int, DeliveryMethod> methodLookup)
    {
        var groups = new Dictionary<string, DeliveryGroupDto>();

        foreach (var row in rows)
        {
            string groupKey;
            string groupTitle;
            int? methodId = null;

            if (row.DeliveryMethodId.HasValue &&
                methodLookup.TryGetValue(row.DeliveryMethodId.Value, out var dm))
            {
                groupKey = $"method-{row.DeliveryMethodId}";
                groupTitle = dm.Name;
                methodId = row.DeliveryMethodId;
            }
            else
            {
                groupKey = "unassigned";
                groupTitle = "לא משויך";
            }

            if (!groups.TryGetValue(groupKey, out var group))
            {
                group = new DeliveryGroupDto
                {
                    Title = groupTitle,
                    DeliveryMethodId = methodId,
                    Orders = []
                };
                groups[groupKey] = group;
            }

            group.Orders.Add(MapToCard(row, methodLookup));
        }

        foreach (var g in groups.Values)
        {
            g.Count = g.Orders.Count;
            g.Orders = g.Orders
                .OrderByDescending(o => o.DisplayTime ?? DateTime.MinValue)
                .ToList();
            g.Orders = StackCards(g.Orders);
        }

        return groups.Values
            .OrderBy(g => g.Title == "לא משויך" ? 1 : 0)
            .ThenBy(g => g.Title)
            .ToList();
    }

    private static OrderCardDto MapToCard(ArchiveOrderRow row, Dictionary<int, DeliveryMethod> methodLookup)
    {
        string? methodName = null;
        if (row.DeliveryMethodId.HasValue &&
            methodLookup.TryGetValue(row.DeliveryMethodId.Value, out var m))
            methodName = m.Name;

        string? packedDuration = null;
        if (row.PackedAt.HasValue)
        {
            var dur = row.PackedAt.Value - row.CreatedAt;
            packedDuration = dur.TotalHours >= 1
                ? $"{(int)dur.TotalHours} שע' {dur.Minutes} דק'"
                : $"{(int)dur.TotalMinutes} דק'";
        }

        return new OrderCardDto
        {
            AppOrderId = row.AppOrderId,
            AccountKey = row.AccountKey,
            AccountName = row.AccountName,
            City = row.City,
            Address = row.Address,
            Phone = row.Phone,
            DisplayTime = row.DisplayTime,
            CurrentStage = row.StageAtDate,
            Pinned = row.Pinned,
            Hidden = row.WasHidden,
            IsManual = row.IsManual,
            ManualNote = row.ManualNote,
            DeliveryMethodId = row.DeliveryMethodId,
            DeliveryMethodName = methodName,
            DeliveryRunId = row.DeliveryRunId,
            NeedsResolve = row.NeedsResolve,
            CreatedAt = row.CreatedAt,
            StageUpdatedAt = row.StageUpdatedAtDate,
            PackedAt = row.PackedAt,
            PackedDuration = packedDuration
        };
    }

    private static List<OrderCardDto> StackCards(List<OrderCardDto> cards)
    {
        var stacked = new List<OrderCardDto>();
        var groups = cards.GroupBy(c => $"{c.AccountKey}|{(c.Address ?? "").Trim().ToLowerInvariant()}");

        foreach (var group in groups)
        {
            var list = group.ToList();
            if (list.Count == 1)
            {
                stacked.Add(list[0]);
                continue;
            }

            var representative = list[0];
            representative.StackCount = list.Count;
            representative.StackedOrderIds = list.Select(c => c.AppOrderId).ToList();
            stacked.Add(representative);
        }

        return stacked;
    }

    /// <summary>Internal row model for the archive CTE query result.</summary>
    private class ArchiveOrderRow
    {
        public int AppOrderId { get; set; }
        public string AccountKey { get; set; } = "";
        public string? AccountName { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public DateTime? DisplayTime { get; set; }
        public bool IsManual { get; set; }
        public string? ManualNote { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? DeliveryMethodId { get; set; }
        public int? DeliveryRunId { get; set; }
        public bool Pinned { get; set; }
        public bool NeedsResolve { get; set; }
        public string StageAtDate { get; set; } = "ORDER_IN_PC";
        public DateTime StageUpdatedAtDate { get; set; }
        public DateTime? PackedAt { get; set; }
        public bool WasHidden { get; set; }
    }
}
