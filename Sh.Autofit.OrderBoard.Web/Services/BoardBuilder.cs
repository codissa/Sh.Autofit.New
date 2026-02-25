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
        ("PACKED", "נארז"),
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

        // Build closest-rule lookup per account
        var accountKeys = orders.Select(o => o.AccountKey).Distinct().ToList();
        var allRules = await _deliveryService.GetRulesForAccountsAsync(accountKeys);
        var closestRuleLookup = BuildClosestRuleLookup(allRules, methodLookup);

        // Dynamic delivery routing: for PACKING orders whose account has 2+
        // delivery methods with time windows, override in-memory to the closest method.
        // Display-only — no DB writes. PACKED orders are never touched.
        ApplyDynamicPackingRouting(orders, allRules, methodLookup);

        // Build columns
        var columns = new List<BoardColumn>();
        foreach (var (stage, label) in Stages)
        {
            var stageOrders = orders.Where(o => o.CurrentStage == stage).ToList();

            // PACKING and PACKED get delivery groups; other stages are flat
            List<DeliveryGroupDto> groups;
            if (stage is "PACKING" or "PACKED")
            {
                groups = BuildGroups(stageOrders, methodLookup, openRuns, closestRuleLookup);
            }
            else
            {
                var cards = stageOrders
                    .OrderByDescending(o => o.DisplayTime ?? DateTime.MinValue)
                    .Select(o => MapToCardDto(o, methodLookup, closestRuleLookup)).ToList();
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

        // Inject empty groups in PACKING and PACKED for active delivery methods
        var now = DateTime.Now.TimeOfDay;
        var groupedColumns = columns.Where(c => c.Stage is "PACKING" or "PACKED");

        foreach (var col in groupedColumns)
        {
            foreach (var method in methods)
            {
                bool shouldShow = method.IsAdHoc
                    || (method.WindowStartTime.HasValue && method.WindowEndTime.HasValue
                        && now >= method.WindowStartTime.Value && now <= method.WindowEndTime.Value);

                if (!shouldShow) continue;

                bool alreadyExists = col.Groups.Any(g => g.DeliveryMethodId == method.DeliveryMethodId);
                if (!alreadyExists)
                {
                    col.Groups.Add(new DeliveryGroupDto
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
            col.Groups = col.Groups
                .OrderBy(g => g.Title == "לא משויך" ? 1 : 0)
                .ThenBy(g => g.Title)
                .ToList();
        }

        // Compute HasPackedSibling: accounts that have orders in PACKED AND in another stage
        var allCards = columns.SelectMany(c => c.Groups.SelectMany(g => g.Orders)).ToList();
        var packedAccounts = new HashSet<string>(
            allCards.Where(c => c.CurrentStage == "PACKED").Select(c => c.AccountKey));
        var otherAccounts = new HashSet<string>(
            allCards.Where(c => c.CurrentStage != "PACKED").Select(c => c.AccountKey));
        var siblingAccounts = new HashSet<string>(packedAccounts.Where(a => otherAccounts.Contains(a)));

        if (siblingAccounts.Count > 0)
        {
            foreach (var card in allCards)
            {
                if (siblingAccounts.Contains(card.AccountKey))
                    card.HasPackedSibling = true;
            }
        }

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
        List<DeliveryRun> openRuns,
        Dictionary<string, (string Name, string Window)?> closestRuleLookup)
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

            group.Orders.Add(MapToCardDto(order, methodLookup, closestRuleLookup));
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

    private static OrderCardDto MapToCardDto(
        AppOrder order,
        Dictionary<int, DeliveryMethod> methodLookup,
        Dictionary<string, (string Name, string Window)?> closestRuleLookup)
    {
        string? methodName = null;
        if (order.DeliveryMethodId.HasValue && methodLookup.TryGetValue(order.DeliveryMethodId.Value, out var m))
            methodName = m.Name;

        closestRuleLookup.TryGetValue(order.AccountKey, out var ruleInfo);

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
            NeedsResolve = order.NeedsResolve,
            CreatedAt = order.CreatedAt,
            StageUpdatedAt = order.StageUpdatedAt,
            ClosestRuleName = ruleInfo?.Name,
            ClosestRuleWindow = ruleInfo?.Window
        };
    }

    private static Dictionary<string, (string Name, string Window)?> BuildClosestRuleLookup(
        List<DeliveryMethodCustomerRule> allRules,
        Dictionary<int, DeliveryMethod> methodLookup)
    {
        var now = DateTime.Now.TimeOfDay;
        var lookup = new Dictionary<string, (string Name, string Window)?>();

        var byAccount = allRules.GroupBy(r => r.AccountKey);
        foreach (var group in byAccount)
        {
            var accountRules = group
                .Where(r => r.WindowStart.HasValue && r.WindowEnd.HasValue)
                .ToList();

            if (accountRules.Count == 0)
            {
                lookup[group.Key] = null;
                continue;
            }

            DeliveryMethodCustomerRule? closest = null;
            var closestDist = TimeSpan.MaxValue;

            foreach (var rule in accountRules)
            {
                var start = rule.WindowStart!.Value;
                var end = rule.WindowEnd!.Value;

                TimeSpan dist;
                if (now >= start && now <= end)
                    dist = TimeSpan.Zero; // currently active window
                else if (now < start)
                    dist = start - now;   // upcoming window today
                else
                    dist = (TimeSpan.FromHours(24) - now) + start; // wraps to tomorrow

                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = rule;
                }
            }

            if (closest != null && methodLookup.TryGetValue(closest.DeliveryMethodId, out var method))
            {
                lookup[group.Key] = (
                    method.Name,
                    $"{closest.WindowStart:hh\\:mm}-{closest.WindowEnd:hh\\:mm}"
                );
            }
            else
            {
                lookup[group.Key] = null;
            }
        }

        return lookup;
    }

    /// <summary>
    /// For PACKING orders whose account has rules for 2+ delivery methods with time windows,
    /// override DeliveryMethodId in-memory to the closest method's window.
    /// No DB writes — purely visual. PACKED orders are never touched.
    /// </summary>
    private static void ApplyDynamicPackingRouting(
        List<AppOrder> orders,
        List<DeliveryMethodCustomerRule> allRules,
        Dictionary<int, DeliveryMethod> methodLookup)
    {
        // Build effective method per account (only accounts with 2+ timed methods)
        var effectiveLookup = new Dictionary<string, int>();
        foreach (var group in allRules.GroupBy(r => r.AccountKey))
        {
            var effectiveId = DeliveryService.ComputeClosestTimedMethodId(
                group.Select(r => r.DeliveryMethodId), methodLookup);
            if (effectiveId != null)
                effectiveLookup[group.Key] = effectiveId.Value;
        }

        if (effectiveLookup.Count == 0) return;

        // Apply override to PACKING orders only
        foreach (var order in orders)
        {
            if (order.CurrentStage != "PACKING") continue;
            if (!effectiveLookup.TryGetValue(order.AccountKey, out var methodId)) continue;
            order.DeliveryMethodId = methodId;
            order.DeliveryRunId = null;
        }
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
