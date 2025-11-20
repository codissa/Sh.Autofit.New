using Sh.Autofit.New.PartsMappingUI.Models;
using System.Net.Http;
using System.Text.Json;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public class GovernmentApiService : IGovernmentApiService
{
    private const string API_BASE_URL = "https://data.gov.il/api/3/action/datastore_search";
    private const string PRIMARY_RESOURCE_ID = "053cea08-09bc-40ec-8f7a-156f0677aff3";
    private const string FALLBACK_RESOURCE_ID = "f6efe89a-fb3d-43a4-bb61-9bf12a9b9099";
    private const string FALLBACK_RESOURCE_ID_2 = "cd3acc5c-03c3-4c89-9c54-d40f93c0d790";
    private const string OFF_ROAD_VEHICLES_RESOURCE_ID = "6f6acd03-f351-4a8f-8ecf-df792f4f573a";
    private const string PERSONAL_IMPORT_RESOURCE_ID = "03adc637-b6fe-402b-9937-7c3d3afc9140";

    private readonly HttpClient _httpClient;

    public GovernmentApiService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<GovernmentVehicleRecord?> LookupVehicleByPlateAsync(string plateNumber)
    {
        try
        {
            // Clean plate number (remove spaces, dashes, etc.)
            var cleanPlate = CleanPlateNumber(plateNumber);

            if (string.IsNullOrWhiteSpace(cleanPlate))
                return null;

            // Try primary resource first - return immediately if found
            var record = await TryLookupAsync(cleanPlate, PRIMARY_RESOURCE_ID);
            if (record != null)
                return record;

            // If not found, try first fallback resource - return immediately if found
            record = await TryLookupAsync(cleanPlate, FALLBACK_RESOURCE_ID);
            if (record != null)
                return record;

            // If still not found, try second fallback resource - return immediately if found
            record = await TryLookupAsync(cleanPlate, FALLBACK_RESOURCE_ID_2);
            if (record != null)
                return record;

            // If still not found, try personal import database - return immediately if found
            record = await TryLookupAsync(cleanPlate, PERSONAL_IMPORT_RESOURCE_ID);
            if (record != null)
                return record;

            // If still not found, try off-road vehicles database (last resort)
            record = await TryLookupAsync(cleanPlate, OFF_ROAD_VEHICLES_RESOURCE_ID);
            if (record != null)
                return record;

            // Not found in any resource
            return null;
        }
        catch (Exception ex)
        {
            // Log error (you can add logging here)
            throw new Exception($"Failed to lookup vehicle from government API: {ex.Message}", ex);
        }
    }

    private async Task<GovernmentVehicleRecord?> TryLookupAsync(string cleanPlate, string resourceId)
    {
        try
        {
            // Build API URL with limit=1 to only fetch the first match (faster response)
            var url = $"{API_BASE_URL}?resource_id={resourceId}&q={cleanPlate}&limit=1";

            // Make API request
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return null;

            // Parse response
            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<GovernmentApiResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Return first record if available
            if (apiResponse?.Success == true &&
                apiResponse.Result?.Records != null &&
                apiResponse.Result.Records.Count > 0)
            {
                return apiResponse.Result.Records[0];
            }

            return null;
        }
        catch
        {
            // Silently fail and let the calling method try the next resource
            return null;
        }
    }

    public async Task<bool> IsVehicleOffRoadAsync(string plateNumber)
    {
        try
        {
            // Clean plate number (remove spaces, dashes, etc.)
            var cleanPlate = CleanPlateNumber(plateNumber);

            if (string.IsNullOrWhiteSpace(cleanPlate))
                return false;

            // Build API URL for off-road vehicles database (limit=1 for faster response)
            var url = $"{API_BASE_URL}?resource_id={OFF_ROAD_VEHICLES_RESOURCE_ID}&q={cleanPlate}&limit=1";

            // Make API request
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return false;

            // Parse response
            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<GovernmentApiResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // If vehicle is found in off-road database, return true
            return apiResponse?.Success == true &&
                   apiResponse.Result?.Records != null &&
                   apiResponse.Result.Records.Count > 0;
        }
        catch
        {
            // If API fails, assume vehicle is not off-road (don't block the flow)
            return false;
        }
    }

    public async Task<bool> IsPersonalImportAsync(string plateNumber)
    {
        try
        {
            // Clean plate number (remove spaces, dashes, etc.)
            var cleanPlate = CleanPlateNumber(plateNumber);

            if (string.IsNullOrWhiteSpace(cleanPlate))
                return false;

            // Build API URL for personal import vehicles database (limit=1 for faster response)
            var url = $"{API_BASE_URL}?resource_id={PERSONAL_IMPORT_RESOURCE_ID}&q={cleanPlate}&limit=1";

            // Make API request
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return false;

            // Parse response
            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<GovernmentApiResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // If vehicle is found in personal import database, return true
            return apiResponse?.Success == true &&
                   apiResponse.Result?.Records != null &&
                   apiResponse.Result.Records.Count > 0;
        }
        catch
        {
            // If API fails, assume vehicle is not personal import (don't block the flow)
            return false;
        }
    }

    private string CleanPlateNumber(string plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber))
            return string.Empty;

        // Remove spaces, dashes, and other common separators
        return plateNumber.Replace("-", "")
                         .Replace(" ", "")
                         .Replace(".", "")
                         .Trim();
    }
}
