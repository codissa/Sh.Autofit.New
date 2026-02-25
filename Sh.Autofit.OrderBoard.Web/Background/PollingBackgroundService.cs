using Microsoft.AspNetCore.SignalR;
using Sh.Autofit.OrderBoard.Web.Hubs;
using Sh.Autofit.OrderBoard.Web.Models;
using Sh.Autofit.OrderBoard.Web.Services;

namespace Sh.Autofit.OrderBoard.Web.Background;

public class PollingBackgroundService : BackgroundService
{
    private readonly ISh2013PollingService _poller;
    private readonly IAppOrderService _orderService;
    private readonly IStageEngine _stageEngine;
    private readonly IDeliveryService _deliveryService;
    private readonly IMergeService _mergeService;
    private readonly IHubContext<BoardHub> _hubContext;
    private readonly ILogger<PollingBackgroundService> _logger;

    public PollingBackgroundService(
        ISh2013PollingService poller,
        IAppOrderService orderService,
        IStageEngine stageEngine,
        IDeliveryService deliveryService,
        IMergeService mergeService,
        IHubContext<BoardHub> hubContext,
        ILogger<PollingBackgroundService> logger)
    {
        _poller = poller;
        _orderService = orderService;
        _stageEngine = stageEngine;
        _deliveryService = deliveryService;
        _mergeService = mergeService;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderBoard polling service started");

        // Wait a bit on startup to let everything initialize
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var changed = await PollCycleAsync();
                if (changed.Count > 0)
                {
                    await _hubContext.Clients.All.SendAsync("board.diff",
                        new { changedOrderIds = changed, timestamp = DateTime.Now },
                        stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in polling cycle");
            }

            var intervalStr = await _orderService.GetSettingAsync("PollingIntervalSeconds", "30");
            var interval = int.TryParse(intervalStr, out var s) ? s : 30;
            await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
        }

        _logger.LogInformation("OrderBoard polling service stopped");
    }

    private async Task<List<int>> PollCycleAsync()
    {
        var changedOrderIds = new HashSet<int>();
        var now = DateTime.Now;

        // 1. Load tracked links and settings
        var presentLinks = await _orderService.GetAllPresentLinksAsync();
        var trackedIds = presentLinks.Select(l => l.StockId).ToList();
        var lastMaxIdStr = await _orderService.GetSettingAsync("LastMaxStockId", "0");
        var lastMaxId = int.TryParse(lastMaxIdStr, out var lmi) ? lmi : 0;
        var thresholdStr = await _orderService.GetSettingAsync("DisappearanceThresholdPolls", "3");
        var threshold = int.TryParse(thresholdStr, out var t) ? t : 3;

        // 2. Query 1: Discover new rows
        var newRows = await _poller.DiscoverNewRowsAsync(lastMaxId);
        _logger.LogDebug("Discovered {Count} new stock rows (lastMaxId={LastMaxId})", newRows.Count, lastMaxId);

        // 3. Query 2: Re-check tracked IDs
        var recheckRows = await _poller.RecheckTrackedIdsAsync(trackedIds);
        var recheckLookup = recheckRows.ToDictionary(r => r.ID);

        // 4. Process new rows
        var maxIdSeen = lastMaxId;
        foreach (var row in newRows)
        {
            if (row.ID > maxIdSeen) maxIdSeen = row.ID;
            var orderId = await ProcessNewStockRowAsync(row, now);
            if (orderId.HasValue) changedOrderIds.Add(orderId.Value);
        }

        if (maxIdSeen > lastMaxId)
        {
            await _orderService.SetSettingAsync("LastMaxStockId", maxIdSeen.ToString());
        }

        // 5. Process re-checked rows (status changes + disappearance)
        foreach (var link in presentLinks)
        {
            if (recheckLookup.TryGetValue(link.StockId, out var freshRow))
            {
                // Still present — update status
                bool statusChanged = link.Status != freshRow.Status;
                link.LastSeenAt = now;
                link.Status = freshRow.Status;
                link.MissCount = 0;
                await _orderService.UpdateLinkAsync(link);

                if (statusChanged)
                {
                    changedOrderIds.Add(link.AppOrderId);
                }
            }
            else
            {
                // Missing — increment miss counter
                link.MissCount++;
                if (link.MissCount >= threshold)
                {
                    link.IsPresent = false;
                    link.DisappearedAt = now;
                    _logger.LogInformation("Stock ID {StockId} disappeared (missed {Count} polls)", link.StockId, link.MissCount);
                }
                await _orderService.UpdateLinkAsync(link);
                changedOrderIds.Add(link.AppOrderId);
            }
        }

        // 6. Recompute stages for changed orders
        foreach (var orderId in changedOrderIds.ToList())
        {
            await RecomputeStageAsync(orderId, now);
        }

        // 7. Auto-merge and auto-assign for newly created orders
        foreach (var orderId in changedOrderIds.ToList())
        {
            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (order == null) continue;

            await _mergeService.TryAutoMergeAsync(order, _orderService);
            await _deliveryService.AutoAssignDeliveryAsync(order, _orderService);
        }

        // 8. Correlate ORDER_PRINTED ↔ DOC_IN_PC for same account+address
        foreach (var orderId in changedOrderIds.ToList())
        {
            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (order == null || order.Hidden) continue;

            var absorbedId = await _mergeService.TryCorrelateStagesAsync(order, _orderService);
            if (absorbedId.HasValue)
            {
                changedOrderIds.Add(absorbedId.Value);
                // Recompute stage for the DOC_IN_PC order that absorbed new links
                await RecomputeStageAsync(orderId, now);
            }
        }

        // 9. Check for expired delivery method windows
        var expiredIds = await CheckExpiredWindowsAsync();
        foreach (var id in expiredIds)
            changedOrderIds.Add(id);

        if (changedOrderIds.Count > 0)
        {
            _logger.LogInformation("Poll cycle: {Count} orders changed", changedOrderIds.Count);
        }

        return changedOrderIds.ToList();
    }

    private async Task<List<int>> CheckExpiredWindowsAsync()
    {
        var changed = new List<int>();
        var methods = await _deliveryService.GetActiveMethodsAsync();
        var allOrders = await _orderService.GetAllOrdersAsync(includeHidden: false);
        var now = DateTime.Now;

        foreach (var method in methods)
        {
            if (method.IsAdHoc) continue;
            if (!method.WindowStartTime.HasValue || !method.WindowEndTime.HasValue) continue;

            var assignedOrders = allOrders
                .Where(o => o.DeliveryMethodId == method.DeliveryMethodId
                         && !o.Hidden
                         && o.CurrentStage == "PACKED")
                .ToList();

            if (assignedOrders.Count == 0) continue;

            var windowEnd = method.WindowEndTime.Value;
            var hiddenCount = 0;

            foreach (var order in assignedOrders)
            {
                // Compute when this specific order's delivery window expires.
                // If packed before/during the window → expires same day.
                // If packed after the window ended → it's for tomorrow's occurrence.
                var packedAtLocal = order.StageUpdatedAt;

                DateTime expiresAt;
                if (packedAtLocal.TimeOfDay <= windowEnd)
                    expiresAt = packedAtLocal.Date + windowEnd;      // same day
                else
                    expiresAt = packedAtLocal.Date.AddDays(1) + windowEnd; // next day

                if (now < expiresAt) continue; // not yet expired for this order

                order.Hidden = true;
                order.HiddenReason = $"Window expired for '{method.Name}'";
                order.HiddenAt = DateTime.Now;
                await _orderService.UpdateOrderAsync(order);

                await _orderService.InsertStageEventAsync(new StageEvent
                {
                    AppOrderId = order.AppOrderId,
                    Actor = "system",
                    Action = "HIDE",
                    Payload = $"{{\"reason\":\"window_expired\",\"methodId\":{method.DeliveryMethodId}}}"
                });

                changed.Add(order.AppOrderId);
                hiddenCount++;
            }

            if (hiddenCount > 0)
            {
                _logger.LogInformation("Window expired for method '{Name}': hid {Count} orders",
                    method.Name, hiddenCount);

                await _hubContext.Clients.All.SendAsync("delivery.updated",
                    new { action = "window_expired", deliveryMethodId = method.DeliveryMethodId });
            }
        }

        return changed;
    }

    private async Task<int?> ProcessNewStockRowAsync(StockRow row, DateTime now)
    {
        // Check if we already have this StockId
        var existingLink = await _orderService.GetLinkByStockIdAsync(row.ID);
        if (existingLink != null)
        {
            // Already tracked — update
            existingLink.Status = row.Status;
            existingLink.LastSeenAt = now;
            existingLink.MissCount = 0;
            await _orderService.UpdateLinkAsync(existingLink);
            return existingLink.AppOrderId;
        }

        // Find or create the AppOrder this stock row belongs to
        int? appOrderId = null;

        if (row.DocumentID == 11)
        {
            // This IS an order. Find by existing link with same DocNumber, or create new.
            var existingOrder = await _orderService.FindOrderByDocNumberAsync(row.DocNumber);
            if (existingOrder != null)
            {
                appOrderId = existingOrder.AppOrderId;
            }
            else
            {
                // Create new AppOrder
                var newOrder = new AppOrder
                {
                    AccountKey = row.AccountKey ?? "",
                    AccountName = row.AccountName,
                    City = row.City,
                    Address = row.Address,
                    Phone = row.Phone,
                    DisplayTime = row.IssueDate ?? row.ValueDate,
                    CurrentStage = "ORDER_IN_PC",
                    StageUpdatedAt = now,
                    IsManual = false
                };
                appOrderId = await _orderService.CreateOrderAsync(newOrder);

                _logger.LogInformation("Created AppOrder #{Id} for DocNumber={DocNumber} Account={Account}",
                    appOrderId, row.DocNumber, row.AccountKey);
            }
        }
        else
        {
            // This is a doc (1/4/7). Link to order via Reference -> DocNumber
            if (row.Reference.HasValue && row.Reference.Value > 0)
            {
                var parentOrder = await _orderService.FindOrderByDocNumberAsync(row.Reference.Value);
                if (parentOrder != null)
                {
                    appOrderId = parentOrder.AppOrderId;
                }
                else
                {
                    // The parent order might not exist yet. Create a placeholder AppOrder.
                    var placeholderOrder = new AppOrder
                    {
                        AccountKey = row.AccountKey ?? "",
                        AccountName = row.AccountName,
                        City = row.City,
                        Address = row.Address,
                        Phone = row.Phone,
                        DisplayTime = row.IssueDate ?? row.ValueDate,
                        CurrentStage = "DOC_IN_PC",
                        StageUpdatedAt = now,
                        IsManual = false
                    };
                    appOrderId = await _orderService.CreateOrderAsync(placeholderOrder);

                    _logger.LogInformation("Created placeholder AppOrder #{Id} for doc Reference={Ref}",
                        appOrderId, row.Reference);
                }
            }
            else
            {
                // No reference — create standalone order
                var standaloneOrder = new AppOrder
                {
                    AccountKey = row.AccountKey ?? "",
                    AccountName = row.AccountName,
                    City = row.City,
                    Address = row.Address,
                    Phone = row.Phone,
                    DisplayTime = row.IssueDate ?? row.ValueDate,
                    CurrentStage = "DOC_IN_PC",
                    StageUpdatedAt = now,
                    IsManual = false,
                    NeedsResolve = true
                };
                appOrderId = await _orderService.CreateOrderAsync(standaloneOrder);

                _logger.LogWarning("Doc StockId={StockId} DocID={DocId} has no Reference — created standalone AppOrder #{Id}",
                    row.ID, row.DocumentID, appOrderId);
            }
        }

        if (!appOrderId.HasValue) return null;

        // Create the link
        var link = new AppOrderLink
        {
            AppOrderId = appOrderId.Value,
            StockId = row.ID,
            DocumentId = row.DocumentID,
            DocNumber = row.DocNumber,
            Status = row.Status,
            Reference = row.Reference,
            IsPresent = true,
            MissCount = 0
        };
        await _orderService.CreateLinkAsync(link);

        return appOrderId;
    }

    private async Task RecomputeStageAsync(int appOrderId, DateTime now)
    {
        var order = await _orderService.GetOrderByIdAsync(appOrderId);
        if (order == null || order.Hidden) return;

        // PACKED is a manual stage — never overwrite it with auto-computed stage
        if (order.CurrentStage == "PACKED") return;

        var links = await _orderService.GetLinksForOrderAsync(appOrderId);
        var newStage = _stageEngine.ComputeStage(links);

        if (newStage != order.CurrentStage)
        {
            var oldStage = order.CurrentStage;
            order.CurrentStage = newStage;
            order.StageUpdatedAt = now;
            await _orderService.UpdateOrderAsync(order);

            await _orderService.InsertStageEventAsync(new StageEvent
            {
                AppOrderId = appOrderId,
                Actor = "system",
                Action = "MOVE_STAGE",
                FromStage = oldStage,
                ToStage = newStage
            });

            _logger.LogInformation("AppOrder #{Id} stage: {From} -> {To}", appOrderId, oldStage, newStage);
        }
    }
}
