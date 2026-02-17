using Microsoft.AspNetCore.SignalR;

namespace Sh.Autofit.OrderBoard.Web.Hubs;

public class BoardHub : Hub
{
    private readonly ILogger<BoardHub> _logger;

    public BoardHub(ILogger<BoardHub> logger)
    {
        _logger = logger;
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("OrderBoard client connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("OrderBoard client disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
