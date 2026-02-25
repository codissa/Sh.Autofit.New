using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Sh.Autofit.OrderBoard.Web.Hubs;
using Sh.Autofit.OrderBoard.Web.Models;
using Sh.Autofit.OrderBoard.Web.Models.Dtos;
using Sh.Autofit.OrderBoard.Web.Services;

namespace Sh.Autofit.OrderBoard.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IAppOrderService _orderService;
    private readonly IDeliveryService _deliveryService;
    private readonly IHubContext<BoardHub> _hubContext;

    private static readonly HashSet<string> ValidStages = ["ORDER_IN_PC", "ORDER_PRINTED", "DOC_IN_PC", "PACKING", "PACKED"];

    public OrdersController(IAppOrderService orderService, IDeliveryService deliveryService, IHubContext<BoardHub> hubContext)
    {
        _orderService = orderService;
        _deliveryService = deliveryService;
        _hubContext = hubContext;
    }

    [HttpPost("manual")]
    public async Task<IActionResult> CreateManualOrder([FromBody] CreateManualOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AccountKey))
            return BadRequest(new { error = "AccountKey is required" });

        var order = new AppOrder
        {
            AccountKey = request.AccountKey.Trim(),
            AccountName = request.AccountName?.Trim(),
            City = request.City?.Trim(),
            Address = request.Address?.Trim(),
            DisplayTime = request.DisplayTime ?? DateTime.Now,
            CurrentStage = "ORDER_IN_PC",
            StageUpdatedAt = DateTime.Now,
            IsManual = true,
            ManualNote = request.Note?.Trim()
        };

        var id = await _orderService.CreateOrderAsync(order);

        await _orderService.InsertStageEventAsync(new StageEvent
        {
            AppOrderId = id,
            Actor = $"client:{HttpContext.Connection.RemoteIpAddress}",
            Action = "CREATE_MANUAL",
            ToStage = "ORDER_IN_PC"
        });

        await _hubContext.Clients.All.SendAsync("order.updated", new { orderId = id, action = "created" });

        return Ok(new { appOrderId = id });
    }

    [HttpPost("bulk-hide")]
    public async Task<IActionResult> BulkHideByStage([FromBody] BulkHideRequest request)
    {
        if (!ValidStages.Contains(request.Stage))
            return BadRequest(new { error = "Invalid stage" });

        var count = await _orderService.BulkHideByStageAsync(request.Stage);

        await _orderService.InsertStageEventAsync(new StageEvent
        {
            Actor = $"client:{HttpContext.Connection.RemoteIpAddress}",
            Action = "BULK_HIDE",
            Payload = $"{{\"stage\":\"{request.Stage}\",\"count\":{count}}}"
        });

        await _hubContext.Clients.All.SendAsync("order.updated", new { action = "bulk_hidden", stage = request.Stage, count });

        return Ok(new { count });
    }

    [HttpPost("{id:int}/move")]
    public async Task<IActionResult> MoveOrder(int id, [FromBody] MoveOrderRequest request)
    {
        if (!ValidStages.Contains(request.ToStage))
            return BadRequest(new { error = "Invalid stage" });

        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null) return NotFound();

        var oldStage = order.CurrentStage;

        // When packing (→ PACKED), lock in the effective delivery method
        // so the order persists in the correct group
        if (request.ToStage == "PACKED")
        {
            var effectiveMethodId = await _deliveryService.GetEffectiveDeliveryMethodIdAsync(order.AccountKey);
            if (effectiveMethodId.HasValue)
            {
                order.DeliveryMethodId = effectiveMethodId.Value;
                order.DeliveryRunId = null;
            }
        }

        order.CurrentStage = request.ToStage;
        order.StageUpdatedAt = DateTime.Now;
        await _orderService.UpdateOrderAsync(order);

        await _orderService.InsertStageEventAsync(new StageEvent
        {
            AppOrderId = id,
            Actor = $"client:{HttpContext.Connection.RemoteIpAddress}",
            Action = "MOVE_STAGE",
            FromStage = oldStage,
            ToStage = request.ToStage
        });

        await _hubContext.Clients.All.SendAsync("order.updated", new { orderId = id, action = "moved" });

        return Ok();
    }

    [HttpPost("{id:int}/hide")]
    public async Task<IActionResult> HideOrder(int id, [FromBody] HideOrderRequest? request = null)
    {
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null) return NotFound();

        order.Hidden = true;
        order.HiddenReason = request?.Reason;
        order.HiddenAt = DateTime.Now;
        await _orderService.UpdateOrderAsync(order);

        await _orderService.InsertStageEventAsync(new StageEvent
        {
            AppOrderId = id,
            Actor = $"client:{HttpContext.Connection.RemoteIpAddress}",
            Action = "HIDE"
        });

        await _hubContext.Clients.All.SendAsync("order.updated", new { orderId = id, action = "hidden" });

        return Ok();
    }

    [HttpPost("{id:int}/unhide")]
    public async Task<IActionResult> UnhideOrder(int id)
    {
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null) return NotFound();

        order.Hidden = false;
        order.HiddenReason = null;
        order.HiddenAt = null;
        await _orderService.UpdateOrderAsync(order);

        await _orderService.InsertStageEventAsync(new StageEvent
        {
            AppOrderId = id,
            Actor = $"client:{HttpContext.Connection.RemoteIpAddress}",
            Action = "UNHIDE"
        });

        await _hubContext.Clients.All.SendAsync("order.updated", new { orderId = id, action = "unhidden" });

        return Ok();
    }

    [HttpPost("{id:int}/pin")]
    public async Task<IActionResult> PinOrder(int id)
    {
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null) return NotFound();

        order.Pinned = true;
        await _orderService.UpdateOrderAsync(order);

        await _orderService.InsertStageEventAsync(new StageEvent
        {
            AppOrderId = id,
            Actor = $"client:{HttpContext.Connection.RemoteIpAddress}",
            Action = "PIN"
        });

        await _hubContext.Clients.All.SendAsync("order.updated", new { orderId = id, action = "pinned" });

        return Ok();
    }

    [HttpPost("{id:int}/unpin")]
    public async Task<IActionResult> UnpinOrder(int id)
    {
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null) return NotFound();

        order.Pinned = false;
        await _orderService.UpdateOrderAsync(order);

        await _orderService.InsertStageEventAsync(new StageEvent
        {
            AppOrderId = id,
            Actor = $"client:{HttpContext.Connection.RemoteIpAddress}",
            Action = "UNPIN"
        });

        await _hubContext.Clients.All.SendAsync("order.updated", new { orderId = id, action = "unpinned" });

        return Ok();
    }

    [HttpPost("{id:int}/assign-delivery")]
    public async Task<IActionResult> AssignDelivery(int id, [FromBody] AssignDeliveryRequest request)
    {
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null) return NotFound();

        order.DeliveryMethodId = request.DeliveryMethodId;
        order.DeliveryRunId = request.DeliveryRunId;
        await _orderService.UpdateOrderAsync(order);

        // Auto-create customer rule for scheduled (non-ad-hoc) methods
        // so the assignment persists for all future orders from this customer
        if (request.DeliveryMethodId.HasValue)
        {
            var method = await _deliveryService.GetMethodByIdAsync(request.DeliveryMethodId.Value);
            if (method != null && !method.IsAdHoc)
            {
                var existingRules = await _deliveryService.GetRulesForAccountAsync(order.AccountKey);
                bool alreadyHasRule = existingRules.Any(r => r.DeliveryMethodId == request.DeliveryMethodId.Value);

                if (!alreadyHasRule)
                {
                    await _deliveryService.CreateRuleAsync(new DeliveryMethodCustomerRule
                    {
                        AccountKey = order.AccountKey,
                        DeliveryMethodId = request.DeliveryMethodId.Value,
                        IsActive = true
                    });
                }
            }
        }

        await _orderService.InsertStageEventAsync(new StageEvent
        {
            AppOrderId = id,
            Actor = $"client:{HttpContext.Connection.RemoteIpAddress}",
            Action = "ASSIGN_DELIVERY",
            Payload = System.Text.Json.JsonSerializer.Serialize(request)
        });

        await _hubContext.Clients.All.SendAsync("order.updated", new { orderId = id, action = "delivery_assigned" });

        return Ok();
    }
}
