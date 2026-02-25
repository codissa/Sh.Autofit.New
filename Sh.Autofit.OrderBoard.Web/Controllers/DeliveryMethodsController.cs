using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Sh.Autofit.OrderBoard.Web.Hubs;
using Sh.Autofit.OrderBoard.Web.Models;
using Sh.Autofit.OrderBoard.Web.Models.Dtos;
using Sh.Autofit.OrderBoard.Web.Services;

namespace Sh.Autofit.OrderBoard.Web.Controllers;

[ApiController]
[Route("api/delivery-methods")]
public class DeliveryMethodsController : ControllerBase
{
    private readonly IDeliveryService _deliveryService;
    private readonly IAppOrderService _orderService;
    private readonly IHubContext<BoardHub> _hubContext;

    public DeliveryMethodsController(
        IDeliveryService deliveryService,
        IAppOrderService orderService,
        IHubContext<BoardHub> hubContext)
    {
        _deliveryService = deliveryService;
        _orderService = orderService;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetMethods()
    {
        var methods = await _deliveryService.GetActiveMethodsAsync();
        return Ok(methods);
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAllMethods([FromQuery] bool includeInactive = true)
    {
        var methods = await _deliveryService.GetAllMethodsAsync(includeInactive);
        return Ok(methods);
    }

    [HttpPost]
    public async Task<IActionResult> CreateMethod([FromBody] CreateDeliveryMethodRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name is required" });

        var method = new DeliveryMethod
        {
            Name = request.Name.Trim(),
            IsAdHoc = request.IsAdHoc,
            IsActive = true,
            RulesJson = request.RulesJson,
            AutoHideAfterMinutes = request.AutoHideAfterMinutes,
            WindowStartTime = ParseTime(request.WindowStartTime),
            WindowEndTime = ParseTime(request.WindowEndTime)
        };

        var id = await _deliveryService.CreateMethodAsync(method);

        await _hubContext.Clients.All.SendAsync("delivery.updated", new { action = "method_created", deliveryMethodId = id });

        return Ok(new { deliveryMethodId = id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateMethod(int id, [FromBody] UpdateDeliveryMethodRequest request)
    {
        var method = await _deliveryService.GetMethodByIdAsync(id);
        if (method == null) return NotFound();

        method.Name = request.Name.Trim();
        method.IsAdHoc = request.IsAdHoc;
        method.WindowStartTime = ParseTime(request.WindowStartTime);
        method.WindowEndTime = ParseTime(request.WindowEndTime);
        method.AutoHideAfterMinutes = request.AutoHideAfterMinutes;

        await _deliveryService.UpdateMethodAsync(method);

        await _hubContext.Clients.All.SendAsync("delivery.updated",
            new { action = "method_updated", deliveryMethodId = id });

        return Ok();
    }

    [HttpPost("{id:int}/reactivate")]
    public async Task<IActionResult> ReactivateMethod(int id)
    {
        var method = await _deliveryService.GetMethodByIdAsync(id);
        if (method == null) return NotFound();

        await _deliveryService.ReactivateMethodAsync(id);

        await _hubContext.Clients.All.SendAsync("delivery.updated",
            new { action = "method_reactivated", deliveryMethodId = id });

        return Ok();
    }

    [HttpPost("{id:int}/close")]
    public async Task<IActionResult> CloseMethod(int id)
    {
        var method = await _deliveryService.GetMethodByIdAsync(id);
        if (method == null) return NotFound();

        await _deliveryService.CloseMethodAsync(id);

        // Archive all orders assigned to this method
        var allOrders = await _orderService.GetAllOrdersAsync(includeHidden: false);
        var assignedOrders = allOrders.Where(o => o.DeliveryMethodId == id).ToList();

        foreach (var order in assignedOrders)
        {
            order.Hidden = true;
            order.HiddenReason = $"Method '{method.Name}' closed";
            order.HiddenAt = DateTime.Now;
            await _orderService.UpdateOrderAsync(order);

            await _orderService.InsertStageEventAsync(new StageEvent
            {
                AppOrderId = order.AppOrderId,
                Actor = "system",
                Action = "HIDE",
                Payload = $"{{\"reason\":\"method_closed\",\"methodId\":{id}}}"
            });
        }

        await _hubContext.Clients.All.SendAsync("delivery.updated", new { action = "method_closed", deliveryMethodId = id });

        return Ok();
    }

    private static TimeSpan? ParseTime(string? value)
        => !string.IsNullOrWhiteSpace(value) && TimeSpan.TryParse(value, out var ts) ? ts : null;
}
