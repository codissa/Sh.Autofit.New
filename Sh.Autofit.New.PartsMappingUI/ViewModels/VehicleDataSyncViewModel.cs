using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sh.Autofit.New.PartsMappingUI.Services;
using System.Windows;

namespace Sh.Autofit.New.PartsMappingUI.ViewModels;

public partial class VehicleDataSyncViewModel : ObservableObject
{
    private readonly IVehicleDataSyncService _syncService;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "מוכן לסנכרון";

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private int _totalRecords;

    [ObservableProperty]
    private string _syncResultSummary = string.Empty;

    [ObservableProperty]
    private string _syncDetails = string.Empty;

    [ObservableProperty]
    private bool _canSync = true;

    [ObservableProperty]
    private bool _canCancel;

    // Stats
    [ObservableProperty]
    private string _quantityStats = "לא סונכרנו עדיין";

    [ObservableProperty]
    private string _registrationStats = "לא סונכרנו עדיין";

    [ObservableProperty]
    private string _wltpStats = "לא סונכרנו עדיין";

    [ObservableProperty]
    private string _vehicleTypeCount = "-";

    [ObservableProperty]
    private string _consolidatedModelCount = "-";

    public VehicleDataSyncViewModel(IVehicleDataSyncService syncService)
    {
        _syncService = syncService;
        _ = LoadStatsAsync();
    }

    private async Task LoadStatsAsync()
    {
        try
        {
            var stats = await _syncService.GetSyncStatsAsync();

            VehicleTypeCount = stats.VehicleTypeCount.ToString("N0");
            ConsolidatedModelCount = stats.ConsolidatedModelCount.ToString("N0");

            // Quantity stats
            if (stats.QuantityCount > 0)
            {
                var lastQty = stats.LastSyncByDataset.GetValueOrDefault("VehicleQuantities");
                QuantityStats = $"{stats.QuantityCount:N0} רשומות" +
                    (lastQty != default ? $" | עדכון אחרון: {lastQty:dd/MM/yyyy HH:mm}" : "");
            }

            // Registration stats
            if (stats.TotalRegistrationCount > 0)
            {
                var details = string.Join(", ",
                    stats.RegistrationCountByResource.Select(kv => $"{kv.Key}: {kv.Value:N0}"));
                RegistrationStats = $"{stats.TotalRegistrationCount:N0} רשומות ({details})";
            }

            // WLTP stats
            var lastWltp = stats.LastSyncByDataset
                .Where(kv => kv.Key.StartsWith("WLTP") || kv.Key == "VehicleSpecs")
                .Select(kv => kv.Value)
                .DefaultIfEmpty()
                .Max();
            if (lastWltp != default)
            {
                WltpStats = $"עדכון אחרון: {lastWltp:dd/MM/yyyy HH:mm}";
            }
        }
        catch
        {
            // Stats loading failure is non-critical
        }
    }

    [RelayCommand(CanExecute = nameof(CanSync))]
    private async Task SyncAllDataAsync()
    {
        await RunSyncAsync("סנכרון מלא (כל המאגרים)", async (ct) =>
            await _syncService.SyncAllDataAsync(
                fullRefresh: false,
                progressCallback: ReportProgress,
                cancellationToken: ct));
    }

    [RelayCommand(CanExecute = nameof(CanSync))]
    private async Task FullRefreshAsync()
    {
        var confirm = MessageBox.Show(
            "האם אתה בטוח שברצונך למחוק את כל הנתונים המקומיים ולהוריד הכל מחדש?\n\n" +
            "פעולה זו עשויה לקחת זמן רב (30-60 דקות).",
            "אישור רענון מלא",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        await RunSyncAsync("רענון מלא (הורדה מחדש)", async (ct) =>
            await _syncService.SyncAllDataAsync(
                fullRefresh: true,
                progressCallback: ReportProgress,
                cancellationToken: ct));
    }

    [RelayCommand(CanExecute = nameof(CanSync))]
    private async Task SyncWltpOnlyAsync()
    {
        await RunSyncAsync("סנכרון מפרטי WLTP", async (ct) =>
            await _syncService.SyncAllVehiclesAsync(
                progressCallback: ReportProgress,
                cancellationToken: ct));
    }

    [RelayCommand(CanExecute = nameof(CanSync))]
    private async Task SyncQuantitiesOnlyAsync()
    {
        await RunSyncAsync("סנכרון כמויות רכבים", async (ct) =>
            await _syncService.SyncVehicleQuantitiesAsync(
                progressCallback: ReportProgress,
                cancellationToken: ct));
    }

    [RelayCommand(CanExecute = nameof(CanSync))]
    private async Task SyncRegistrationsOnlyAsync()
    {
        await RunSyncAsync("סנכרון רישומי רכבים", async (ct) =>
            await _syncService.SyncVehicleRegistrationsAsync(
                fullRefresh: false,
                progressCallback: ReportProgress,
                cancellationToken: ct));
    }

    private async Task RunSyncAsync(string operationName, Func<CancellationToken, Task<VehicleDataSyncResult>> syncFunc)
    {
        try
        {
            IsLoading = true;
            CanSync = false;
            CanCancel = true;
            SyncResultSummary = string.Empty;
            SyncDetails = string.Empty;
            Progress = 0;
            TotalRecords = 0;

            _cancellationTokenSource = new CancellationTokenSource();
            StatusMessage = $"מתחיל {operationName}...";

            var result = await syncFunc(_cancellationTokenSource.Token);

            if (_cancellationTokenSource.Token.IsCancellationRequested)
            {
                StatusMessage = "הסנכרון בוטל על ידי המשתמש";
                SyncResultSummary = "הסנכרון בוטל";
            }
            else
            {
                StatusMessage = $"{operationName} — הושלם בהצלחה!";
                SyncResultSummary = result.GetSummary();
                SyncDetails = BuildDetails(result);

                MessageBox.Show(
                    $"{operationName} הושלם!\n\n{result.GetSummary()}",
                    "סנכרון הושלם",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            // Refresh stats
            await LoadStatsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה: {ex.Message}";
            MessageBox.Show(
                $"שגיאה ב{operationName}:\n{ex.Message}",
                "שגיאה",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
            CanSync = true;
            CanCancel = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private void ReportProgress(int current, int total, string message)
    {
        Progress = current;
        TotalRecords = total;
        StatusMessage = message;
    }

    private static string BuildDetails(VehicleDataSyncResult result)
    {
        var details = "פרטי סנכרון:\n\n";
        details += $"סה\"כ רשומות שעובדו: {result.TotalRecordsProcessed:N0}\n";
        details += $"רכבים שהותאמו: {result.VehiclesMatched:N0}\n";
        details += $"רכבים שעודכנו: {result.VehiclesUpdated:N0}\n";
        details += $"רכבים חדשים שנוצרו: {result.NewVehiclesFound:N0}\n";
        details += $"רכבים שלא נמצאו: {result.VehiclesNotMatched:N0}\n";
        details += $"זמן ביצוע: {result.Duration.TotalMinutes:F1} דקות\n";

        if (result.Errors.Count > 0)
        {
            details += $"\nשגיאות ({result.Errors.Count}):\n";
            foreach (var error in result.Errors.Take(10))
                details += $"- {error}\n";
            if (result.Errors.Count > 10)
                details += $"...ועוד {result.Errors.Count - 10} שגיאות\n";
        }

        return details;
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void CancelSync()
    {
        _cancellationTokenSource?.Cancel();
        StatusMessage = "מבטל סנכרון...";
    }
}
