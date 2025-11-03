using Sh.Autofit.New.PartsMappingUI.Models;
using System.Net.Http;
using System.Text.Json;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public class GovernmentApiService : IGovernmentApiService
{
    private const string API_BASE_URL = "https://data.gov.il/api/3/action/datastore_search";
    private const string RESOURCE_ID = "053cea08-09bc-40ec-8f7a-156f0677aff3";

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

            // Build API URL
            var url = $"{API_BASE_URL}?resource_id={RESOURCE_ID}&q={cleanPlate}";

            // Make API request
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

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
        catch (Exception ex)
        {
            // Log error (you can add logging here)
            throw new Exception($"Failed to lookup vehicle from government API: {ex.Message}", ex);
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
