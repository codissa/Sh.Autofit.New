using System.Data.SqlClient;
using Dapper;
using Sh.Autofit.OrderBoard.Web.Models;

namespace Sh.Autofit.OrderBoard.Web.Services;

public interface IDeliveryService
{
    Task<List<DeliveryMethod>> GetActiveMethodsAsync();
    Task<List<DeliveryMethod>> GetAllMethodsAsync(bool includeInactive = false);
    Task<DeliveryMethod?> GetMethodByIdAsync(int id);
    Task<int> CreateMethodAsync(DeliveryMethod method);
    Task UpdateMethodAsync(DeliveryMethod method);
    Task CloseMethodAsync(int id);
    Task ReactivateMethodAsync(int id);

    Task<List<DeliveryRun>> GetOpenRunsAsync();
    Task<List<DeliveryRun>> GetRunsByMethodAsync(int methodId);
    Task<DeliveryRun?> GetRunByIdAsync(int id);
    Task<int> CreateRunAsync(DeliveryRun run);
    Task CloseRunAsync(int id);

    Task<List<DeliveryMethodCustomerRule>> GetAllRulesAsync();
    Task<List<DeliveryMethodCustomerRule>> GetRulesForAccountAsync(string accountKey);
    Task<List<DeliveryMethodCustomerRule>> GetRulesForMethodAsync(int deliveryMethodId);
    Task<DeliveryMethodCustomerRule?> GetRuleByIdAsync(int id);
    Task<int> CreateRuleAsync(DeliveryMethodCustomerRule rule);
    Task UpdateRuleAsync(DeliveryMethodCustomerRule rule);
    Task DeactivateRuleAsync(int id);
    Task AutoAssignDeliveryAsync(AppOrder order, IAppOrderService orderService);
    Task<List<DeliveryMethodCustomerRule>> GetRulesForAccountsAsync(IEnumerable<string> accountKeys);
    Task<int?> GetEffectiveDeliveryMethodIdAsync(string accountKey);
}

public class DeliveryService : IDeliveryService
{
    private readonly string _connectionString;

    public DeliveryService(string connectionString)
    {
        _connectionString = connectionString;
    }

    private SqlConnection CreateConnection() => new(_connectionString);

    // ---- DeliveryMethods ----

    public async Task<List<DeliveryMethod>> GetActiveMethodsAsync()
    {
        const string sql = "SELECT * FROM dbo.DeliveryMethods WHERE IsActive = 1 ORDER BY Name";
        using var conn = CreateConnection();
        await conn.OpenAsync();
        return (await conn.QueryAsync<DeliveryMethod>(sql)).ToList();
    }

    public async Task<List<DeliveryMethod>> GetAllMethodsAsync(bool includeInactive = false)
    {
        var sql = includeInactive
            ? "SELECT * FROM dbo.DeliveryMethods ORDER BY IsActive DESC, Name"
            : "SELECT * FROM dbo.DeliveryMethods WHERE IsActive = 1 ORDER BY Name";
        using var conn = CreateConnection();
        await conn.OpenAsync();
        return (await conn.QueryAsync<DeliveryMethod>(sql)).ToList();
    }

    public async Task<DeliveryMethod?> GetMethodByIdAsync(int id)
    {
        const string sql = "SELECT * FROM dbo.DeliveryMethods WHERE DeliveryMethodId = @Id";
        using var conn = CreateConnection();
        await conn.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<DeliveryMethod>(sql, new { Id = id });
    }

    public async Task<int> CreateMethodAsync(DeliveryMethod method)
    {
        const string sql = @"
            INSERT INTO dbo.DeliveryMethods (Name, IsActive, IsAdHoc, CreatedAt, ClosedAt, RulesJson, AutoHideAfterMinutes, WindowStartTime, WindowEndTime)
            OUTPUT INSERTED.DeliveryMethodId
            VALUES (@Name, @IsActive, @IsAdHoc, SYSDATETIME(), @ClosedAt, @RulesJson, @AutoHideAfterMinutes, @WindowStartTime, @WindowEndTime)";

        using var conn = CreateConnection();
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(sql, method);
    }

    public async Task UpdateMethodAsync(DeliveryMethod method)
    {
        const string sql = @"
            UPDATE dbo.DeliveryMethods SET
                Name = @Name, IsAdHoc = @IsAdHoc,
                WindowStartTime = @WindowStartTime, WindowEndTime = @WindowEndTime,
                AutoHideAfterMinutes = @AutoHideAfterMinutes, RulesJson = @RulesJson
            WHERE DeliveryMethodId = @DeliveryMethodId";

        using var conn = CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(sql, method);
    }

    public async Task CloseMethodAsync(int id)
    {
        const string sql = @"
            UPDATE dbo.DeliveryMethods SET IsActive = 0, ClosedAt = SYSDATETIME()
            WHERE DeliveryMethodId = @Id";

        using var conn = CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(sql, new { Id = id });
    }

    public async Task ReactivateMethodAsync(int id)
    {
        const string sql = "UPDATE dbo.DeliveryMethods SET IsActive = 1, ClosedAt = NULL WHERE DeliveryMethodId = @Id";
        using var conn = CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(sql, new { Id = id });
    }

    // ---- DeliveryRuns ----

    public async Task<List<DeliveryRun>> GetOpenRunsAsync()
    {
        const string sql = "SELECT * FROM dbo.DeliveryRuns WHERE State = 'OPEN' ORDER BY WindowStart";
        using var conn = CreateConnection();
        await conn.OpenAsync();
        return (await conn.QueryAsync<DeliveryRun>(sql)).ToList();
    }

    public async Task<List<DeliveryRun>> GetRunsByMethodAsync(int methodId)
    {
        const string sql = "SELECT * FROM dbo.DeliveryRuns WHERE DeliveryMethodId = @MethodId ORDER BY WindowStart DESC";
        using var conn = CreateConnection();
        await conn.OpenAsync();
        return (await conn.QueryAsync<DeliveryRun>(sql, new { MethodId = methodId })).ToList();
    }

    public async Task<DeliveryRun?> GetRunByIdAsync(int id)
    {
        const string sql = "SELECT * FROM dbo.DeliveryRuns WHERE DeliveryRunId = @Id";
        using var conn = CreateConnection();
        await conn.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<DeliveryRun>(sql, new { Id = id });
    }

    public async Task<int> CreateRunAsync(DeliveryRun run)
    {
        const string sql = @"
            INSERT INTO dbo.DeliveryRuns (DeliveryMethodId, WindowStart, WindowEnd, State, ClosedAt)
            OUTPUT INSERTED.DeliveryRunId
            VALUES (@DeliveryMethodId, @WindowStart, @WindowEnd, 'OPEN', NULL)";

        using var conn = CreateConnection();
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(sql, run);
    }

    public async Task CloseRunAsync(int id)
    {
        const string sql = @"
            UPDATE dbo.DeliveryRuns SET State = 'CLOSED', ClosedAt = SYSDATETIME()
            WHERE DeliveryRunId = @Id";

        using var conn = CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(sql, new { Id = id });
    }

    // ---- Customer Rules ----

    public async Task<List<DeliveryMethodCustomerRule>> GetAllRulesAsync()
    {
        const string sql = "SELECT * FROM dbo.DeliveryMethodCustomerRules ORDER BY AccountKey, DeliveryMethodId";
        using var conn = CreateConnection();
        await conn.OpenAsync();
        return (await conn.QueryAsync<DeliveryMethodCustomerRule>(sql)).ToList();
    }

    public async Task<List<DeliveryMethodCustomerRule>> GetRulesForAccountAsync(string accountKey)
    {
        const string sql = @"
            SELECT * FROM dbo.DeliveryMethodCustomerRules
            WHERE AccountKey = @AccountKey AND IsActive = 1";

        using var conn = CreateConnection();
        await conn.OpenAsync();
        return (await conn.QueryAsync<DeliveryMethodCustomerRule>(sql, new { AccountKey = accountKey })).ToList();
    }

    public async Task<List<DeliveryMethodCustomerRule>> GetRulesForMethodAsync(int deliveryMethodId)
    {
        const string sql = "SELECT * FROM dbo.DeliveryMethodCustomerRules WHERE DeliveryMethodId = @MethodId ORDER BY AccountKey";
        using var conn = CreateConnection();
        await conn.OpenAsync();
        return (await conn.QueryAsync<DeliveryMethodCustomerRule>(sql, new { MethodId = deliveryMethodId })).ToList();
    }

    public async Task<DeliveryMethodCustomerRule?> GetRuleByIdAsync(int id)
    {
        const string sql = "SELECT * FROM dbo.DeliveryMethodCustomerRules WHERE Id = @Id";
        using var conn = CreateConnection();
        await conn.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<DeliveryMethodCustomerRule>(sql, new { Id = id });
    }

    public async Task<int> CreateRuleAsync(DeliveryMethodCustomerRule rule)
    {
        const string sql = @"
            INSERT INTO dbo.DeliveryMethodCustomerRules (AccountKey, DeliveryMethodId, WindowStart, WindowEnd, DaysOfWeek, IsActive)
            OUTPUT INSERTED.Id
            VALUES (@AccountKey, @DeliveryMethodId, @WindowStart, @WindowEnd, @DaysOfWeek, @IsActive)";

        using var conn = CreateConnection();
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(sql, rule);
    }

    public async Task UpdateRuleAsync(DeliveryMethodCustomerRule rule)
    {
        const string sql = @"
            UPDATE dbo.DeliveryMethodCustomerRules SET
                DeliveryMethodId = @DeliveryMethodId,
                WindowStart = @WindowStart, WindowEnd = @WindowEnd,
                DaysOfWeek = @DaysOfWeek, IsActive = @IsActive
            WHERE Id = @Id";

        using var conn = CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(sql, rule);
    }

    public async Task DeactivateRuleAsync(int id)
    {
        const string sql = "UPDATE dbo.DeliveryMethodCustomerRules SET IsActive = 0 WHERE Id = @Id";
        using var conn = CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<List<DeliveryMethodCustomerRule>> GetRulesForAccountsAsync(IEnumerable<string> accountKeys)
    {
        var keys = accountKeys.ToList();
        if (keys.Count == 0) return [];

        const string sql = "SELECT * FROM dbo.DeliveryMethodCustomerRules WHERE AccountKey IN @Keys AND IsActive = 1";
        using var conn = CreateConnection();
        await conn.OpenAsync();
        return (await conn.QueryAsync<DeliveryMethodCustomerRule>(sql, new { Keys = keys })).ToList();
    }

    // ---- Effective delivery (dynamic routing) ----

    /// <summary>
    /// For accounts with rules for 2+ active delivery methods that have time windows,
    /// compute which method's window is currently closest (active or next upcoming).
    /// Returns null if the account has fewer than 2 timed methods.
    /// </summary>
    public static int? ComputeClosestTimedMethodId(
        IEnumerable<int> candidateMethodIds,
        Dictionary<int, DeliveryMethod> methodLookup)
    {
        var now = DateTime.Now.TimeOfDay;

        var timedMethods = candidateMethodIds
            .Distinct()
            .Where(id => methodLookup.TryGetValue(id, out var m)
                         && m.WindowStartTime.HasValue && m.WindowEndTime.HasValue)
            .Select(id => methodLookup[id])
            .ToList();

        if (timedMethods.Count < 2) return null;

        DeliveryMethod? closest = null;
        var closestDist = TimeSpan.MaxValue;

        foreach (var method in timedMethods)
        {
            var start = method.WindowStartTime!.Value;
            var end = method.WindowEndTime!.Value;

            TimeSpan dist;
            if (now >= start && now <= end)
                dist = end - now; // active — prefer the one expiring soonest
            else if (now < start)
                dist = start - now; // upcoming today
            else
                dist = (TimeSpan.FromHours(24) - now) + start; // wraps to next day

            if (dist < closestDist)
            {
                closestDist = dist;
                closest = method;
            }
        }

        return closest?.DeliveryMethodId;
    }

    /// <summary>
    /// Single-account version: loads rules and methods, returns effective delivery method ID.
    /// </summary>
    public async Task<int?> GetEffectiveDeliveryMethodIdAsync(string accountKey)
    {
        var rules = await GetRulesForAccountAsync(accountKey);
        if (rules.Count == 0) return null;

        var methods = await GetActiveMethodsAsync();
        var methodLookup = methods.ToDictionary(m => m.DeliveryMethodId);

        return ComputeClosestTimedMethodId(
            rules.Select(r => r.DeliveryMethodId),
            methodLookup);
    }

    // ---- Auto-assignment ----

    public async Task AutoAssignDeliveryAsync(AppOrder order, IAppOrderService orderService)
    {
        if (order.DeliveryMethodId.HasValue) return; // already assigned

        var rules = await GetRulesForAccountAsync(order.AccountKey);
        if (rules.Count == 0) return;

        // For scheduled (non-ad-hoc) methods: the rule is a persistent assignment,
        // always assign regardless of time window on the rule.
        // For ad-hoc methods: match by time window if specified on the rule.
        var methods = await GetActiveMethodsAsync();
        var methodLookup = methods.ToDictionary(m => m.DeliveryMethodId);

        DeliveryMethodCustomerRule? matchingRule = null;

        // First try: find a rule for a scheduled method (persistent — always matches)
        matchingRule = rules.FirstOrDefault(r =>
            methodLookup.TryGetValue(r.DeliveryMethodId, out var m) && !m.IsAdHoc);

        // Fallback: find a rule for an ad-hoc method matching by time window
        if (matchingRule == null)
        {
            var timeOfDay = order.DisplayTime?.TimeOfDay;
            if (timeOfDay != null)
            {
                matchingRule = rules.FirstOrDefault(r =>
                    r.WindowStart.HasValue && r.WindowEnd.HasValue &&
                    timeOfDay >= r.WindowStart.Value && timeOfDay <= r.WindowEnd.Value);
            }
        }

        if (matchingRule == null) return;

        order.DeliveryMethodId = matchingRule.DeliveryMethodId;

        // Check for open run in that method
        var openRuns = await GetOpenRunsAsync();
        var matchingRun = openRuns.FirstOrDefault(r =>
            r.DeliveryMethodId == matchingRule.DeliveryMethodId &&
            order.DisplayTime >= r.WindowStart && order.DisplayTime <= r.WindowEnd);

        if (matchingRun != null)
        {
            order.DeliveryRunId = matchingRun.DeliveryRunId;
        }

        await orderService.UpdateOrderAsync(order);
    }
}
