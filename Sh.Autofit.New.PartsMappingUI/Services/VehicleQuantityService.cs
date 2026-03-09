using System.Net.Http;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public class VehicleQuantityService : IVehicleQuantityService
{
    private readonly HttpClient _httpClient;
    private readonly IDbContextFactory<ShAutofitContext> _contextFactory;
    private const string BaseUrl = "https://data.gov.il/api/3/action/datastore_search";
    private const string ResourceId = "5e87a7a1-2f6f-41c1-8aec-7216d52a6cf6";
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public VehicleQuantityService(HttpClient httpClient, IDbContextFactory<ShAutofitContext> contextFactory)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
        _contextFactory = contextFactory;
    }

    public async Task<VehicleCountResult?> GetVehicleCountAsync(int manufacturerCode, int modelCode, string modelName)
    {
        // Try local DB first
        var localResult = await GetFromLocalDbAsync(manufacturerCode, modelCode, modelName);
        if (localResult != null)
            return localResult;

        // Fall back to API
        return await GetFromApiAsync(manufacturerCode, modelCode, modelName);
    }

    public async Task<Dictionary<(int ManufacturerCode, int ModelCode, string ModelName), VehicleCountResult>> GetVehicleCountBatchAsync(
        IEnumerable<(int ManufacturerCode, int ModelCode, string ModelName)> modelKeys)
    {
        var result = new Dictionary<(int, int, string), VehicleCountResult>();
        var keysToFetch = new List<(int ManufacturerCode, int ModelCode, string ModelName)>();

        // Try all from local DB first
        await using var context = await _contextFactory.CreateDbContextAsync();
        var hasLocalData = await context.LocalVehicleQuantities.AnyAsync();

        if (hasLocalData)
        {
            foreach (var key in modelKeys.Distinct())
            {
                var records = await context.LocalVehicleQuantities
                    .Where(q => q.TozeretCd == key.ManufacturerCode &&
                                q.DegemCd == key.ModelCode &&
                                q.DegemNm == key.ModelName)
                    .ToListAsync();

                if (records.Count > 0)
                {
                    var active = records.Sum(r => r.MisparRechavimPailim);
                    var inactive = records.Sum(r => r.MisparRechavimLePailim);
                    result[key] = new VehicleCountResult(active, inactive, active + inactive);
                }
                else
                {
                    keysToFetch.Add(key);
                }
            }
        }
        else
        {
            // No local data — fetch all from API
            keysToFetch.AddRange(modelKeys.Distinct());
        }

        // Fetch remaining from API
        if (keysToFetch.Count > 0)
        {
            var semaphore = new SemaphoreSlim(3);
            var tasks = keysToFetch.Select(async key =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var count = await GetFromApiAsync(key.ManufacturerCode, key.ModelCode, key.ModelName);
                    if (count != null)
                    {
                        lock (result)
                        {
                            result[key] = count;
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        return result;
    }

    private async Task<VehicleCountResult?> GetFromLocalDbAsync(int manufacturerCode, int modelCode, string modelName)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Check if we have local data at all
            if (!await context.LocalVehicleQuantities.AnyAsync())
                return null;

            var records = await context.LocalVehicleQuantities
                .Where(q => q.TozeretCd == manufacturerCode &&
                            q.DegemCd == modelCode &&
                            q.DegemNm == modelName)
                .ToListAsync();

            if (records.Count == 0)
                return new VehicleCountResult(0, 0, 0);

            var active = records.Sum(r => r.MisparRechavimPailim);
            var inactive = records.Sum(r => r.MisparRechavimLePailim);
            return new VehicleCountResult(active, inactive, active + inactive);
        }
        catch
        {
            return null;
        }
    }

    private async Task<VehicleCountResult?> GetFromApiAsync(int manufacturerCode, int modelCode, string modelName)
    {
        try
        {
            var filters = JsonSerializer.Serialize(new { tozeret_cd = manufacturerCode, degem_cd = modelCode, degem_nm = modelName });
            var url = $"{BaseUrl}?resource_id={ResourceId}&filters={Uri.EscapeDataString(filters)}&limit=1000";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<VehicleQuantityApiResponse>(json, _jsonOptions);

            if (apiResponse?.Success == true && apiResponse.Result?.Records != null)
            {
                var active = apiResponse.Result.Records.Sum(r => r.ActiveVehicleCount);
                var inactive = apiResponse.Result.Records.Sum(r => r.InactiveVehicleCount);
                return new VehicleCountResult(active, inactive, active + inactive);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
