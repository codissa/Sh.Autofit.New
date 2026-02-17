using Sh.Autofit.OrderBoard.Web.Models;
using Sh.Autofit.OrderBoard.Web.Models.Dtos;

namespace Sh.Autofit.OrderBoard.Web.Services;

public interface IBoardBuilder
{
    Task<BoardResponse> BuildBoardAsync(bool includeArchived = false);
}

public class BoardBuilder : IBoardBuilder
{
    private readonly IAppOrderService _orderService;
    private readonly IDeliveryService _deliveryService;

    private static readonly (string Stage, string Label)[] Stages =
    [
        ("ORDER_IN_PC", "הזמנה במחשב"),
        ("ORDER_PRINTED", "הזמנה הודפסה"),
        ("DOC_IN_PC", "מסמך במחשב"),
        ("PACKING", "אריזה"),
    ];

    public BoardBuilder(IAppOrderService orderService, IDeliveryService deliveryService)
    {
        _orderService = orderService;
        _deliveryService = deliveryService;
    }

    public async Task<BoardResponse> BuildBoardAsync(bool includeArchived = false)
    {
        var orders = await _orderService.GetAllOrdersAsync(includeHidden: includeArchived);
        var methods = await _deliveryService.GetActiveMethodsAsync();
        var openRuns = await _deliveryService.GetOpenRunsAsync();

        // Build method lookup
        var methodLookup = methods.ToDictionary(m => m.DeliveryMethodId);

        // Build columns
        var columns = new List<BoardColumn>();
        foreach (var (stage, label) in Stages)
        {
            var stageOrders = orders.Where(o => o.CurrentStage == stage).ToList();

            // Only PACKING gets delivery groups; other stages are flat
            List<DeliveryGroupDto> groups;
            if (stage == "PACKING")
            {
                groups = BuildGroups(stageOrders, methodLookup, openRuns);
            }
            else
            {
                var cards = stageOrders
                    .OrderByDescending(o => o.DisplayTime ?? DateTime.MinValue)
                    .Select(o => MapToCardDto(o, methodLookup)).ToList();
                cards = StackCards(cards);
                groups =
                [
                    new DeliveryGroupDto
                    {
                        Title = "",
                        Orders = cards,
                        Count = stageOrders.Count
                    }
                ];
            }

            columns.Add(new BoardColumn
            {
                Stage = stage,
                Label = label,
                Count = stageOrders.Count,
                Groups = groups
            });
        }

        // Inject empty groups in PACKING for active delivery methods
        var packingColumn = columns.First(c => c.Stage == "PACKING");
        var now = DateTime.Now.TimeOfDay;

        foreach (var method in methods)
        {
            bool shouldShow = method.IsAdHoc
                || (method.WindowStartTime.HasValue && method.WindowEndTime.HasValue
                    && now >= method.WindowStartTime.Value && now <= method.WindowEndTime.Value);

            if (!shouldShow) continue;

            bool alreadyExists = packingColumn.Groups.Any(g => g.DeliveryMethodId == method.DeliveryMethodId);
            if (!alreadyExists)
            {
                packingColumn.Groups.Add(new DeliveryGroupDto
                {
                    Title = method.Name,
                    DeliveryMethodId = method.DeliveryMethodId,
                    TimeWindow = method.WindowStartTime.HasValue
                        ? $"{method.WindowStartTime:hh\\:mm}-{method.WindowEndTime:hh\\:mm}"
                        : null,
                    Count = 0,
                    Orders = []
                });
            }
        }

        // Re-sort so unassigned stays at the bottom after injection
        packingColumn.Groups = packingColumn.Groups
            .OrderBy(g => g.Title == "לא משויך" ? 1 : 0)
            .ThenBy(g => g.Title)
            .ToList();

        // Build delivery method DTOs
        var methodDtos = methods.Select(m => new DeliveryMethodDto
        {
            DeliveryMethodId = m.DeliveryMethodId,
            Name = m.Name,
            IsAdHoc = m.IsAdHoc,
            IsActive = m.IsActive,
            WindowStartTime = m.WindowStartTime?.ToString(@"hh\:mm"),
            WindowEndTime = m.WindowEndTime?.ToString(@"hh\:mm"),
            Runs = openRuns.Where(r => r.DeliveryMethodId == m.DeliveryMethodId)
                .Select(r => new DeliveryRunDto
                {
                    DeliveryRunId = r.DeliveryRunId,
                    State = r.State,
                    WindowStart = r.WindowStart,
                    WindowEnd = r.WindowEnd
                }).ToList()
        }).ToList();

        return new BoardResponse
        {
            Columns = columns,
            DeliveryMethods = methodDtos,
            Timestamp = DateTime.UtcNow
        };
    }

    private List<DeliveryGroupDto> BuildGroups(
        List<AppOrder> orders,
        Dictionary<int, DeliveryMethod> methodLookup,
        List<DeliveryRun> openRuns)
    {
        var groups = new Dictionary<string, DeliveryGroupDto>();

        foreach (var order in orders)
        {
            string groupKey;
            string groupTitle;
            int? methodId = null;
            int? runId = null;

            if (order.DeliveryRunId.HasValue)
            {
                var run = openRuns.FirstOrDefault(r => r.DeliveryRunId == order.DeliveryRunId.Value);
                var method = run != null && methodLookup.TryGetValue(run.DeliveryMethodId, out var m) ? m : null;
                groupKey = $"run-{order.DeliveryRunId}";
                groupTitle = method != null ? $"{method.Name}" : $"ריצה #{order.DeliveryRunId}";
                methodId = order.DeliveryMethodId;
                runId = order.DeliveryRunId;
            }
            else if (order.DeliveryMethodId.HasValue && methodLookup.TryGetValue(order.DeliveryMethodId.Value, out var dm))
            {
                groupKey = $"method-{order.DeliveryMethodId}";
                groupTitle = dm.Name;
                methodId = order.DeliveryMethodId;
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
                    DeliveryRunId = runId,
                    Orders = []
                };
                groups[groupKey] = group;
            }

            group.Orders.Add(MapToCardDto(order, methodLookup));
        }

        // Update counts
        foreach (var g in groups.Values)
            g.Count = g.Orders.Count;

        // Sort by latest first, then stack cards with same AccountKey+Address
        foreach (var g in groups.Values)
        {
            g.Orders = g.Orders.OrderByDescending(o => o.DisplayTime ?? DateTime.MinValue).ToList();
            g.Orders = StackCards(g.Orders);
        }

        // Delivery groups first, unassigned last
        return groups.Values
            .OrderBy(g => g.Title == "לא משויך" ? 1 : 0)
            .ThenBy(g => g.Title)
            .ToList();
    }

    private static OrderCardDto MapToCardDto(AppOrder order, Dictionary<int, DeliveryMethod> methodLookup)
    {
        string? methodName = null;
        if (order.DeliveryMethodId.HasValue && methodLookup.TryGetValue(order.DeliveryMethodId.Value, out var m))
            methodName = m.Name;

        return new OrderCardDto
        {
            AppOrderId = order.AppOrderId,
            AccountKey = order.AccountKey,
            AccountName = order.AccountName,
            City = order.City,
            Address = order.Address,
            Phone = order.Phone,
            DisplayTime = order.DisplayTime,
            CurrentStage = order.CurrentStage,
            Pinned = order.Pinned,
            Hidden = order.Hidden,
            IsManual = order.IsManual,
            ManualNote = order.ManualNote,
            DeliveryMethodId = order.DeliveryMethodId,
            DeliveryMethodName = methodName,
            DeliveryRunId = order.DeliveryRunId,
            NeedsResolve = order.NeedsResolve
        };
    }

    /// <summary>
    /// Stack/aggregate cards with same AccountKey + Address into one card with count badge.
    /// </summary>
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

            // Use first card as representative, mark as stacked
            var representative = list[0];
            representative.StackCount = list.Count;
            representative.StackedOrderIds = list.Select(c => c.AppOrderId).ToList();
            stacked.Add(representative);
        }

        return stacked;
    }
}
