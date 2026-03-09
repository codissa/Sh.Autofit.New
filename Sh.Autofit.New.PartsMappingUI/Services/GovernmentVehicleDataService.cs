using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public class GovernmentVehicleDataService : IGovernmentVehicleDataService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://data.gov.il/api/3/action/datastore_search";
    private const string ResourceId = "142afde2-6228-49f9-8a29-9b6c3a0cbe40";
    private const int MaxRetries = 3;

    public GovernmentVehicleDataService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // Long timeout for large datasets
    }

    /// <summary>
    /// HTTP GET with automatic retry and exponential backoff (2s, 4s, 8s)
    /// </summary>
    private async Task<HttpResponseMessage> GetWithRetryAsync(string url, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var response = await _httpClient.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();
                return response;
            }
            catch (Exception ex) when (attempt < MaxRetries && !ct.IsCancellationRequested &&
                (ex is HttpRequestException || ex is TaskCanceledException))
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                Debug.WriteLine($"HTTP request failed (attempt {attempt}/{MaxRetries}): {ex.Message}. Retrying in {delay.TotalSeconds}s...");
                await Task.Delay(delay, ct);
            }
        }
        throw new HttpRequestException("Max retries exceeded");
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

            var response = await GetWithRetryAsync(url, cancellationToken);

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

        var response = await GetWithRetryAsync(url, CancellationToken.None);

        var jsonString = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<GovernmentVehicleApiResponse>(jsonString);

        if (apiResponse?.Success == true && apiResponse.Result?.Records != null)
        {
            return apiResponse.Result.Records;
        }

        return new List<GovernmentVehicleDataRecord>();
    }

    public async Task<List<VehicleQuantityRecord>> FetchAllVehicleQuantitiesAsync(
        int batchSize = 5000,
        Action<int, int>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        const string quantityResourceId = "5e87a7a1-2f6f-41c1-8aec-7216d52a6cf6";
        var allRecords = new List<VehicleQuantityRecord>();
        int offset = 0;
        int total = 0;
        bool hasMore = true;

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
            var url = $"{BaseUrl}?resource_id={quantityResourceId}&limit={batchSize}&offset={offset}&sort=_id asc";

            var response = await GetWithRetryAsync(url, cancellationToken);

            var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonSerializer.Deserialize<VehicleQuantityApiResponse>(jsonString);

            if (apiResponse?.Success == true && apiResponse.Result?.Records != null)
            {
                allRecords.AddRange(apiResponse.Result.Records);

                if (total == 0)
                    total = apiResponse.Result.Total;

                progressCallback?.Invoke(allRecords.Count, total);

                hasMore = allRecords.Count < total;
                offset += batchSize;

                if (hasMore)
                    await Task.Delay(100, cancellationToken);
            }
            else
            {
                throw new Exception("Failed to fetch vehicle quantity data from government API");
            }
        }

        return allRecords;
    }

    public async Task FetchRegistrationBatchesAsync(
        string resourceId,
        int batchSize,
        int startOffset,
        Action<int, int>? progressCallback,
        Func<List<JsonElement>, Task> batchProcessor,
        CancellationToken cancellationToken)
    {
        int offset = startOffset;
        int total = 0;
        bool hasMore = true;

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
            var url = $"{BaseUrl}?resource_id={resourceId}&limit={batchSize}&offset={offset}&sort=_id asc";

            var response = await GetWithRetryAsync(url, cancellationToken);

            var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.TryGetProperty("success", out var success) && success.GetBoolean() &&
                root.TryGetProperty("result", out var result))
            {
                if (total == 0 && result.TryGetProperty("total", out var totalEl))
                    total = totalEl.GetInt32();

                if (result.TryGetProperty("records", out var records) &&
                    records.ValueKind == JsonValueKind.Array)
                {
                    var batch = new List<JsonElement>();
                    foreach (var record in records.EnumerateArray())
                    {
                        batch.Add(record.Clone());
                    }

                    if (batch.Count > 0)
                        await batchProcessor(batch);

                    var downloaded = offset - startOffset + batch.Count;
                    var remaining = total - startOffset;
                    progressCallback?.Invoke(downloaded, remaining > 0 ? remaining : total);

                    hasMore = batch.Count == batchSize && (offset + batch.Count) < total;
                    offset += batchSize;

                    if (hasMore)
                        await Task.Delay(100, cancellationToken);
                }
                else
                {
                    hasMore = false;
                }
            }
            else
            {
                throw new Exception($"Failed to fetch registration data from resource {resourceId}");
            }
        }
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
