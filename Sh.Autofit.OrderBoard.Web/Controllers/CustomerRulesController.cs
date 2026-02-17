using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Sh.Autofit.OrderBoard.Web.Hubs;
using Sh.Autofit.OrderBoard.Web.Models;
using Sh.Autofit.OrderBoard.Web.Models.Dtos;
using Sh.Autofit.OrderBoard.Web.Services;

namespace Sh.Autofit.OrderBoard.Web.Controllers;

[ApiController]
[Route("api/customer-rules")]
public class CustomerRulesController : ControllerBase
{
    private readonly IDeliveryService _deliveryService;
    private readonly IAccountsService _accountsService;
    private readonly IHubContext<BoardHub> _hubContext;

    public CustomerRulesController(
        IDeliveryService deliveryService,
        IAccountsService accountsService,
        IHubContext<BoardHub> hubContext)
    {
        _deliveryService = deliveryService;
        _accountsService = accountsService;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var rules = await _deliveryService.GetAllRulesAsync();
        return Ok(rules);
    }

    [HttpGet("by-method/{methodId:int}")]
    public async Task<IActionResult> GetByMethod(int methodId)
    {
        var rules = await _deliveryService.GetRulesForMethodAsync(methodId);
        return Ok(rules);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRuleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AccountKey))
            return BadRequest(new { error = "AccountKey is required" });

        var method = await _deliveryService.GetMethodByIdAsync(request.DeliveryMethodId);
        if (method == null)
            return BadRequest(new { error = "DeliveryMethod not found" });

        var rule = new DeliveryMethodCustomerRule
        {
            AccountKey = request.AccountKey.Trim(),
            DeliveryMethodId = request.DeliveryMethodId,
            WindowStart = ParseTime(request.WindowStart),
            WindowEnd = ParseTime(request.WindowEnd),
            DaysOfWeek = request.DaysOfWeek,
            IsActive = true
        };

        var id = await _deliveryService.CreateRuleAsync(rule);

        await _hubContext.Clients.All.SendAsync("delivery.updated",
            new { action = "rule_created", ruleId = id });

        return Ok(new { id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerRuleRequest request)
    {
        var rule = await _deliveryService.GetRuleByIdAsync(id);
        if (rule == null) return NotFound();

        rule.DeliveryMethodId = request.DeliveryMethodId;
        rule.WindowStart = ParseTime(request.WindowStart);
        rule.WindowEnd = ParseTime(request.WindowEnd);
        rule.DaysOfWeek = request.DaysOfWeek;
        rule.IsActive = request.IsActive;

        await _deliveryService.UpdateRuleAsync(rule);

        await _hubContext.Clients.All.SendAsync("delivery.updated",
            new { action = "rule_updated", ruleId = id });

        return Ok();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var rule = await _deliveryService.GetRuleByIdAsync(id);
        if (rule == null) return NotFound();

        await _deliveryService.DeactivateRuleAsync(id);

        await _hubContext.Clients.All.SendAsync("delivery.updated",
            new { action = "rule_deactivated", ruleId = id });

        return Ok();
    }

    [HttpGet("accounts/search")]
    public async Task<IActionResult> SearchAccounts([FromQuery] string q = "")
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(Array.Empty<object>());

        var results = await _accountsService.SearchAccountsAsync(q);
        return Ok(results);
    }

    private static TimeSpan? ParseTime(string? value)
        => !string.IsNullOrWhiteSpace(value) && TimeSpan.TryParse(value, out var ts) ? ts : null;
}
