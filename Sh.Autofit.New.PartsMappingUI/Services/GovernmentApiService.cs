using Microsoft.EntityFrameworkCore;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Models;
using System.Net.Http;
using System.Text.Json;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public class GovernmentApiService : IGovernmentApiService
{
    private const string API_BASE_URL = "https://data.gov.il/api/3/action/datastore_search";
    private const string PRIMARY_RESOURCE_ID = "053cea08-09bc-40ec-8f7a-156f0677aff3";
    private const string INACTIVE_WITH_CODE_RESOURCE_ID = "f6efe89a-fb3d-43a4-bb61-9bf12a9b9099";
    private const string OFF_ROAD_CANCELLED_RESOURCE_ID = "851ecab1-0622-4dbe-a6c7-f950cf82abf9";
    private const string PERSONAL_IMPORT_RESOURCE_ID = "03adc637-b6fe-402b-9937-7c3d3afc9140";
    private const string INACTIVE_NO_CODE_RESOURCE_ID = "6f6acd03-f351-4a8f-8ecf-df792f4f573a";
    private const string HEAVY_AND_NO_CODE_RESOURCE_ID = "cd3acc5c-03c3-4c89-9c54-d40f93c0d790";

    // Priority order for picking best local record
    private static readonly string[] SourcePriority =
    {
        "Primary", "InactiveWithCode", "OffRoadCancelled", "PersonalImport", "InactiveNoCode", "HeavyAndNoCode"
    };

    private readonly HttpClient _httpClient;
    private readonly IDbContextFactory<ShAutofitContext> _contextFactory;

    public GovernmentApiService(IDbContextFactory<ShAutofitContext> contextFactory)
    {
        _contextFactory = contextFactory;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<GovernmentVehicleRecord?> LookupVehicleByPlateAsync(string plateNumber, CancellationToken ct = default)
    {
        try
        {
            var cleanPlate = CleanPlateNumber(plateNumber);
            if (string.IsNullOrWhiteSpace(cleanPlate))
                return null;

            // Try local DB first
            var localResult = await TryLookupLocalAsync(cleanPlate, ct);
            if (localResult != null)
                return localResult;

            // Fall back to API
            return await LookupVehicleByPlateFromApiAsync(cleanPlate, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to lookup vehicle: {ex.Message}", ex);
        }
    }

    private async Task<GovernmentVehicleRecord?> TryLookupLocalAsync(string cleanPlate, CancellationToken ct = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            // Check if we have local registration data at all
            if (!await context.LocalVehicleRegistrations.AnyAsync(ct))
                return null;

            var localRecords = await context.LocalVehicleRegistrations
                .Where(r => r.MisparRechev == cleanPlate)
                .ToListAsync(ct);

            if (localRecords.Count == 0)
                return null;

            // Pick best record by source priority
            LocalVehicleRegistration? bestRecord = null;
            foreach (var source in SourcePriority)
            {
                bestRecord = localRecords.FirstOrDefault(r => r.SourceResource == source);
                if (bestRecord != null) break;
            }

            bestRecord ??= localRecords[0];

            // Map to GovernmentVehicleRecord
            return new GovernmentVehicleRecord
            {
                Id = bestRecord.GovRecordId ?? 0,
                LicensePlate = long.TryParse(bestRecord.MisparRechev, out var lp) ? lp : 0,
                ManufacturerCode = bestRecord.TozeretCd,
                ModelType = bestRecord.SugDegem,
                ManufacturerName = bestRecord.TozeretNm,
                ModelCode = bestRecord.DegemCd,
                ModelName = bestRecord.DegemNm,
                TrimLevel = bestRecord.RamatGimur,
                PollutionGroup = bestRecord.KvutzatZihum,
                ManufacturingYear = bestRecord.ShnatYitzur,
                EngineModel = bestRecord.DegemManoa,
                LastTestDate = bestRecord.MivchanAcharonDt,
                ValidityDate = bestRecord.TokefDt,
                OwnershipType = bestRecord.Baalut,
                VinChassis = bestRecord.Misgeret,
                VinNumber = bestRecord.MisparShilda ?? bestRecord.Shilda,
                EngineVolume = bestRecord.NefachManoa,
                EngineNumber = bestRecord.MisparManoa,
                ColorCode = bestRecord.TzevaCd,
                ColorName = bestRecord.TzevaRechev,
                FrontTire = bestRecord.ZmigKidmi,
                RearTire = bestRecord.ZmigAhori,
                FuelType = bestRecord.SugDelekNm,
                RegistrationInstruction = bestRecord.HoraatRishum,
                RoadDate = bestRecord.MoedAliyaLakvish,
                CommercialName = bestRecord.KinuyMishari,
                VehicleTypeName = bestRecord.SugRechevNm,
                VehicleTypeCode = bestRecord.SugRechevCd,
                CountryOfOrigin = bestRecord.TozeretEretzNm,
                ImportType = bestRecord.SugYevu,
                IsOffRoad = bestRecord.SourceResource == "OffRoadCancelled",
                IsPersonalImport = bestRecord.SourceResource == "PersonalImport",
                SourceResourceId = bestRecord.SourceResource
            };
        }
        catch
        {
            return null; // Fall back to API on any DB error
        }
    }

    private async Task<GovernmentVehicleRecord?> LookupVehicleByPlateFromApiAsync(string cleanPlate, CancellationToken ct = default)
    {
        // Try resources in priority order
        var record = await TryLookupAsync(cleanPlate, PRIMARY_RESOURCE_ID, ct);
        if (record != null) { record.SourceResourceId = "Primary"; return record; }

        record = await TryLookupAsync(cleanPlate, INACTIVE_WITH_CODE_RESOURCE_ID, ct);
        if (record != null) { record.SourceResourceId = "InactiveWithCode"; return record; }

        record = await TryLookupAsync(cleanPlate, OFF_ROAD_CANCELLED_RESOURCE_ID, ct);
        if (record != null) { record.IsOffRoad = true; record.SourceResourceId = "OffRoadCancelled"; return record; }

        record = await TryLookupAsync(cleanPlate, HEAVY_AND_NO_CODE_RESOURCE_ID, ct);
        if (record != null) { record.SourceResourceId = "HeavyAndNoCode"; return record; }

        record = await TryLookupAsync(cleanPlate, PERSONAL_IMPORT_RESOURCE_ID, ct);
        if (record != null) { record.IsPersonalImport = true; record.SourceResourceId = "PersonalImport"; return record; }

        record = await TryLookupAsync(cleanPlate, INACTIVE_NO_CODE_RESOURCE_ID, ct);
        if (record != null) { record.SourceResourceId = "InactiveNoCode"; return record; }

        return null;
    }

    private async Task<GovernmentVehicleRecord?> TryLookupAsync(string cleanPlate, string resourceId, CancellationToken ct = default)
    {
        try
        {
            var url = $"{API_BASE_URL}?resource_id={resourceId}&q={cleanPlate}&limit=1";
            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<GovernmentApiResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new GovernmentVehicleRecordConverter() }
            });

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
            return null;
        }
    }

    public async Task<bool> IsVehicleOffRoadAsync(string plateNumber, CancellationToken ct = default)
    {
        try
        {
            var cleanPlate = CleanPlateNumber(plateNumber);
            if (string.IsNullOrWhiteSpace(cleanPlate))
                return false;

            // Try local DB first
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync(ct);
                if (await context.LocalVehicleRegistrations.AnyAsync(ct))
                {
                    return await context.LocalVehicleRegistrations
                        .AnyAsync(r => r.MisparRechev == cleanPlate && r.SourceResource == "OffRoadCancelled", ct);
                }
            }
            catch { /* fall through to API */ }

            // Fall back to API
            var url = $"{API_BASE_URL}?resource_id={OFF_ROAD_CANCELLED_RESOURCE_ID}&q={cleanPlate}&limit=1";
            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
                return false;

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<GovernmentApiResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new GovernmentVehicleRecordConverter() }
            });

            return apiResponse?.Success == true &&
                   apiResponse.Result?.Records != null &&
                   apiResponse.Result.Records.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsPersonalImportAsync(string plateNumber, CancellationToken ct = default)
    {
        try
        {
            var cleanPlate = CleanPlateNumber(plateNumber);
            if (string.IsNullOrWhiteSpace(cleanPlate))
                return false;

            // Try local DB first
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync(ct);
                if (await context.LocalVehicleRegistrations.AnyAsync(ct))
                {
                    return await context.LocalVehicleRegistrations
                        .AnyAsync(r => r.MisparRechev == cleanPlate && r.SourceResource == "PersonalImport", ct);
                }
            }
            catch { /* fall through to API */ }

            // Fall back to API
            var url = $"{API_BASE_URL}?resource_id={PERSONAL_IMPORT_RESOURCE_ID}&q={cleanPlate}&limit=1";
            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
                return false;

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<GovernmentApiResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new GovernmentVehicleRecordConverter() }
            });

            if (apiResponse?.Success == true &&
                apiResponse.Result?.Records != null &&
                apiResponse.Result.Records.Count > 0)
            {
                var record = apiResponse.Result.Records[0];
                if (!string.IsNullOrEmpty(record.ImportType))
                    return record.ImportType.Contains("יבוא אישי");
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private string CleanPlateNumber(string plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber))
            return string.Empty;

        return plateNumber.Replace("-", "")
                         .Replace(" ", "")
                         .Replace(".", "")
                         .Trim();
    }
}
