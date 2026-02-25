using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public class VehicleQuantityService : IVehicleQuantityService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://data.gov.il/api/3/action/datastore_search";
    private const string ResourceId = "5e87a7a1-2f6f-41c1-8aec-7216d52a6cf6";

    private static readonly ConcurrentDictionary<(int, int, string), (VehicleCountResult Result, DateTime FetchedAt)> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    public VehicleQuantityService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<VehicleCountResult?> GetVehicleCountAsync(int manufacturerCode, int modelCode, string modelName)
    {
        var key = (manufacturerCode, modelCode, modelName);

        if (_cache.TryGetValue(key, out var cached) && DateTime.UtcNow - cached.FetchedAt < CacheTtl)
            return cached.Result;

        try
        {
            var filters = JsonSerializer.Serialize(new { tozeret_cd = manufacturerCode, degem_cd = modelCode, degem_nm = modelName });
            var url = $"{BaseUrl}?resource_id={ResourceId}&filters={Uri.EscapeDataString(filters)}&limit=1000";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<VehicleQuantityApiResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (apiResponse?.Success == true && apiResponse.Result?.Records != null)
            {
                var active = apiResponse.Result.Records.Sum(r => r.ActiveVehicleCount);
                var inactive = apiResponse.Result.Records.Sum(r => r.InactiveVehicleCount);
                var result = new VehicleCountResult(active, inactive, active + inactive);
                _cache[key] = (result, DateTime.UtcNow);
                return result;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<Dictionary<(int ManufacturerCode, int ModelCode, string ModelName), VehicleCountResult>> GetVehicleCountBatchAsync(
        IEnumerable<(int ManufacturerCode, int ModelCode, string ModelName)> modelKeys)
    {
        var result = new Dictionary<(int, int, string), VehicleCountResult>();
        var keysToFetch = new List<(int ManufacturerCode, int ModelCode, string ModelName)>();

        foreach (var key in modelKeys.Distinct())
        {
            if (_cache.TryGetValue(key, out var cached) && DateTime.UtcNow - cached.FetchedAt < CacheTtl)
                result[key] = cached.Result;
            else
                keysToFetch.Add(key);
        }

        if (keysToFetch.Count > 0)
        {
            var semaphore = new SemaphoreSlim(3);
            var tasks = keysToFetch.Select(async key =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var count = await GetVehicleCountAsync(key.ManufacturerCode, key.ModelCode, key.ModelName);
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
}
