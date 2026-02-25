using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Sh.Autofit.OrderBoard.Web.Hubs;
using Sh.Autofit.OrderBoard.Web.Models;
using Sh.Autofit.OrderBoard.Web.Models.Dtos;
using Sh.Autofit.OrderBoard.Web.Services;

namespace Sh.Autofit.OrderBoard.Web.Controllers;

[ApiController]
[Route("api/delivery-runs")]
public class DeliveryRunsController : ControllerBase
{
    private readonly IDeliveryService _deliveryService;
    private readonly IAppOrderService _orderService;
    private readonly IHubContext<BoardHub> _hubContext;

    public DeliveryRunsController(
        IDeliveryService deliveryService,
        IAppOrderService orderService,
        IHubContext<BoardHub> hubContext)
    {
        _deliveryService = deliveryService;
        _orderService = orderService;
        _hubContext = hubContext;
    }

    [HttpPost("open")]
    public async Task<IActionResult> OpenRun([FromBody] OpenDeliveryRunRequest request)
    {
        var method = await _deliveryService.GetMethodByIdAsync(request.DeliveryMethodId);
        if (method == null)
            return BadRequest(new { error = "DeliveryMethod not found" });

        var run = new DeliveryRun
        {
            DeliveryMethodId = request.DeliveryMethodId,
            WindowStart = request.WindowStart,
            WindowEnd = request.WindowEnd,
            State = "OPEN"
        };

        var id = await _deliveryService.CreateRunAsync(run);

        await _hubContext.Clients.All.SendAsync("delivery.updated", new { action = "run_opened", deliveryRunId = id });

        return Ok(new { deliveryRunId = id });
    }

    [HttpPost("{id:int}/close")]
    public async Task<IActionResult> CloseRun(int id)
    {
        var run = await _deliveryService.GetRunByIdAsync(id);
        if (run == null) return NotFound();

        await _deliveryService.CloseRunAsync(id);

        // Archive all orders assigned to this run
        var allOrders = await _orderService.GetAllOrdersAsync(includeHidden: false);
        var assignedOrders = allOrders.Where(o => o.DeliveryRunId == id).ToList();

        foreach (var order in assignedOrders)
        {
            order.Hidden = true;
            order.HiddenReason = "Run closed";
            order.HiddenAt = DateTime.Now;
            await _orderService.UpdateOrderAsync(order);

            await _orderService.InsertStageEventAsync(new StageEvent
            {
                AppOrderId = order.AppOrderId,
                Actor = "system",
                Action = "CLOSE_RUN",
                Payload = $"{{\"runId\":{id}}}"
            });
        }

        await _hubContext.Clients.All.SendAsync("delivery.updated", new { action = "run_closed", deliveryRunId = id });

        return Ok();
    }
}
