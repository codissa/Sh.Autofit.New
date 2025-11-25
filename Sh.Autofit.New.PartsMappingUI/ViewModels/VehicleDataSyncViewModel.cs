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
    private string _statusMessage = "מוכן לסנכרון נתוני רכבים";

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

    public VehicleDataSyncViewModel(IVehicleDataSyncService syncService)
    {
        _syncService = syncService;
    }

    [RelayCommand(CanExecute = nameof(CanSync))]
    private async Task SyncAllVehiclesAsync()
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

            StatusMessage = "מתחיל סנכרון...";

            var result = await _syncService.SyncAllVehiclesAsync(
                progressCallback: (current, total, message) =>
                {
                    Progress = current;
                    TotalRecords = total;
                    StatusMessage = message;
                },
                cancellationToken: _cancellationTokenSource.Token);

            if (_cancellationTokenSource.Token.IsCancellationRequested)
            {
                StatusMessage = "הסנכרון בוטל על ידי המשתמש";
                SyncResultSummary = "הסנכרון בוטל";
            }
            else
            {
                StatusMessage = "הסנכרון הושלם בהצלחה!";
                SyncResultSummary = result.GetSummary();

                // Build details
                var details = $"פרטי סנכרון:\n\n";
                details += $"סה\"כ רשומות שעובדו: {result.TotalRecordsProcessed:N0}\n";
                details += $"רכבים שהותאמו: {result.VehiclesMatched:N0}\n";
                details += $"רכבים שעודכנו: {result.VehiclesUpdated:N0}\n";
                details += $"רכבים שלא נמצאו: {result.VehiclesNotMatched:N0}\n";
                details += $"זמן ביצוע: {result.Duration.TotalMinutes:F1} דקות\n";

                if (result.Errors.Any())
                {
                    details += $"\nשגיאות ({result.Errors.Count}):\n";
                    foreach (var error in result.Errors.Take(10))
                    {
                        details += $"- {error}\n";
                    }
                    if (result.Errors.Count > 10)
                    {
                        details += $"...ועוד {result.Errors.Count - 10} שגיאות\n";
                    }
                }

                SyncDetails = details;

                MessageBox.Show(
                    $"הסנכרון הושלם בהצלחה!\n\n{result.GetSummary()}",
                    "סנכרון הושלם",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה בסנכרון: {ex.Message}";
            MessageBox.Show(
                $"שגיאה בסנכרון נתונים:\n{ex.Message}",
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

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void CancelSync()
    {
        _cancellationTokenSource?.Cancel();
        StatusMessage = "מבטל סנכרון...";
    }
}
