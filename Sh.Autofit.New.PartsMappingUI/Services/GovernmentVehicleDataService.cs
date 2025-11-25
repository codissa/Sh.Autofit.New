using System.Net.Http;
using System.Text.Json;
using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public class GovernmentVehicleDataService : IGovernmentVehicleDataService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://data.gov.il/api/3/action/datastore_search";
    private const string ResourceId = "142afde2-6228-49f9-8a29-9b6c3a0cbe40";

    public GovernmentVehicleDataService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // Long timeout for large datasets
    }

    public async Task<List<GovernmentVehicleDataRecord>> FetchAllVehicleDataAsync(
        int batchSize = 1000,
        Action<int, int>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var allRecords = new List<GovernmentVehicleDataRecord>();
        int offset = 0;
        int total = 0;
        bool hasMore = true;

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
            var url = $"{BaseUrl}?resource_id={ResourceId}&limit={batchSize}&offset={offset}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonSerializer.Deserialize<GovernmentVehicleApiResponse>(jsonString);

            if (apiResponse?.Success == true && apiResponse.Result?.Records != null)
            {
                var records = apiResponse.Result.Records;
                allRecords.AddRange(records);

                // Update total on first request
                if (total == 0)
                {
                    total = apiResponse.Result.Total;
                }

                // Report progress
                progressCallback?.Invoke(allRecords.Count, total);

                // Check if there are more records
                hasMore = allRecords.Count < total;
                offset += batchSize;

                // Small delay to be respectful to the API
                if (hasMore)
                {
                    await Task.Delay(100, cancellationToken);
                }
            }
            else
            {
                throw new Exception("Failed to fetch data from government API");
            }
        }

        return allRecords;
    }

    public async Task<List<GovernmentVehicleDataRecord>> FetchVehicleByCodesAsync(
        int manufacturerCode,
        string modelCode)
    {
        // Use CKAN filters to query specific vehicle
        var filters = JsonSerializer.Serialize(new
        {
            tozeret_cd = manufacturerCode,
            degem_cd = modelCode
        });

        var url = $"{BaseUrl}?resource_id={ResourceId}&filters={Uri.EscapeDataString(filters)}&limit=1000";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var jsonString = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<GovernmentVehicleApiResponse>(jsonString);

        if (apiResponse?.Success == true && apiResponse.Result?.Records != null)
        {
            return apiResponse.Result.Records;
        }

        return new List<GovernmentVehicleDataRecord>();
    }

    public string ParseDriveType(string? hanaa_nm)
    {
        if (string.IsNullOrWhiteSpace(hanaa_nm))
            return "2WD"; // Default

        var normalized = hanaa_nm.Trim().ToLowerInvariant();

        // 4x4 / 4WD
        if (normalized.Contains("4x4") ||
            normalized.Contains("4wd") ||
            normalized.Contains("ארבעה גלגלים"))
        {
            return "4WD";
        }

        // AWD / כפולה
        if (normalized.Contains("awd") ||
            normalized.Contains("כפולה") ||
            normalized.Contains("הנעה כפולה"))
        {
            return "AWD";
        }

        // FWD / קדמית
        if (normalized.Contains("fwd") ||
            normalized.Contains("קדמית") ||
            normalized.Contains("הנעה קדמית"))
        {
            return "FWD";
        }

        // RWD / אחורית
        if (normalized.Contains("rwd") ||
            normalized.Contains("אחורית") ||
            normalized.Contains("הנעה אחורית"))
        {
            return "RWD";
        }

        // Default to 2WD for "הנעה רגילה" or unknown
        return "2WD";
    }
}
