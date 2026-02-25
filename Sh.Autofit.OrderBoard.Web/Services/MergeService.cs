using Sh.Autofit.OrderBoard.Web.Models;

namespace Sh.Autofit.OrderBoard.Web.Services;

public interface IMergeService
{
    Task TryAutoMergeAsync(AppOrder realOrder, IAppOrderService orderService);
    Task<int?> TryCorrelateStagesAsync(AppOrder order, IAppOrderService orderService);
}

public class MergeService : IMergeService
{
    /// <summary>
    /// When a real AppOrder (from Stock) appears, find an active manual AppOrder
    /// with same AccountKey + Address, not hidden, not merged, and merge it.
    /// </summary>
    public async Task TryAutoMergeAsync(AppOrder realOrder, IAppOrderService orderService)
    {
        if (realOrder.IsManual) return;

        var allOrders = await orderService.GetAllOrdersAsync(includeHidden: false);

        var manualMatch = allOrders.FirstOrDefault(o =>
            o.IsManual &&
            !o.Hidden &&
            o.MergedIntoAppOrderId == null &&
            o.AccountKey == realOrder.AccountKey &&
            NormalizeAddress(o.Address) == NormalizeAddress(realOrder.Address));

        if (manualMatch == null) return;

        // Copy note if real order doesn't have one
        if (string.IsNullOrEmpty(realOrder.ManualNote) && !string.IsNullOrEmpty(manualMatch.ManualNote))
        {
            realOrder.ManualNote = manualMatch.ManualNote;
            await orderService.UpdateOrderAsync(realOrder);
        }

        // Merge: hide manual, set pointer
        manualMatch.MergedIntoAppOrderId = realOrder.AppOrderId;
        manualMatch.Hidden = true;
        manualMatch.HiddenReason = "Merged";
        manualMatch.HiddenAt = DateTime.Now;
        await orderService.UpdateOrderAsync(manualMatch);

        await orderService.InsertStageEventAsync(new StageEvent
        {
            AppOrderId = realOrder.AppOrderId,
            Actor = "system",
            Action = "MERGE_AUTO",
            Payload = $"{{\"mergedManualOrderId\":{manualMatch.AppOrderId}}}"
        });
    }

    /// <summary>
    /// When an order is in DOC_IN_PC, find any ORDER_PRINTED order with the same
    /// AccountKey + Address. If found, absorb it: transfer its links to the DOC_IN_PC
    /// order and hide the ORDER_PRINTED one. Returns the absorbed order's ID if merged.
    /// </summary>
    public async Task<int?> TryCorrelateStagesAsync(AppOrder order, IAppOrderService orderService)
    {
        if (order.CurrentStage != "DOC_IN_PC") return null;

        var allOrders = await orderService.GetAllOrdersAsync(includeHidden: false);

        var printedMatch = allOrders.FirstOrDefault(o =>
            o.AppOrderId != order.AppOrderId &&
            o.CurrentStage == "ORDER_PRINTED" &&
            !o.Hidden &&
            o.MergedIntoAppOrderId == null &&
            o.AccountKey == order.AccountKey &&
            NormalizeAddress(o.Address) == NormalizeAddress(order.Address));

        if (printedMatch == null) return null;

        // Transfer links from ORDER_PRINTED order to this DOC_IN_PC order
        await orderService.ReassignLinksAsync(printedMatch.AppOrderId, order.AppOrderId);

        // Copy manual note if DOC_IN_PC order doesn't have one
        if (string.IsNullOrEmpty(order.ManualNote) && !string.IsNullOrEmpty(printedMatch.ManualNote))
        {
            order.ManualNote = printedMatch.ManualNote;
            await orderService.UpdateOrderAsync(order);
        }

        // Hide the ORDER_PRINTED order
        printedMatch.MergedIntoAppOrderId = order.AppOrderId;
        printedMatch.Hidden = true;
        printedMatch.HiddenReason = "StageCorrelation";
        printedMatch.HiddenAt = DateTime.Now;
        await orderService.UpdateOrderAsync(printedMatch);

        await orderService.InsertStageEventAsync(new StageEvent
        {
            AppOrderId = order.AppOrderId,
            Actor = "system",
            Action = "MERGE_STAGE_CORRELATION",
            Payload = $"{{\"absorbedOrderId\":{printedMatch.AppOrderId}}}"
        });

        return printedMatch.AppOrderId;
    }

    private static string? NormalizeAddress(string? addr)
    {
        return addr?.Trim().ToLowerInvariant();
    }
}
