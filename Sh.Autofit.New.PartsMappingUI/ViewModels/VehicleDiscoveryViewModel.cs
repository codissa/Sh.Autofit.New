using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sh.Autofit.New.PartsMappingUI.Models;
using Sh.Autofit.New.PartsMappingUI.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace Sh.Autofit.New.PartsMappingUI.ViewModels;

public partial class VehicleDiscoveryViewModel : ObservableObject
{
    private readonly IVehicleDiscoveryService _discoveryService;
    private readonly IPendingVehicleReviewService _reviewService;

    public VehicleDiscoveryViewModel(
        IVehicleDiscoveryService discoveryService,
        IPendingVehicleReviewService reviewService)
    {
        _discoveryService = discoveryService;
        _reviewService = reviewService;
    }

    [ObservableProperty]
    private ObservableCollection<PendingVehicleDisplayModel> _pendingVehicles = new();

    [ObservableProperty]
    private bool _isDiscovering = false;

    [ObservableProperty]
    private bool _isProcessing = false;

    [ObservableProperty]
    private string _statusMessage = "Ready to discover new vehicles";

    [ObservableProperty]
    private int _progressCurrent = 0;

    [ObservableProperty]
    private int _progressTotal = 0;

    [ObservableProperty]
    private int _newVehiclesFound = 0;

    [ObservableProperty]
    private int _approvedCount = 0;

    [ObservableProperty]
    private Guid? _currentBatchId = null;

    public bool HasPendingVehicles => PendingVehicles.Any();

    [RelayCommand]
    private async Task DiscoverNewVehiclesAsync()
    {
        try
        {
            IsDiscovering = true;
            StatusMessage = "Starting discovery...";

            var result = await _discoveryService.DiscoverNewVehiclesAsync(
                progressCallback: (current, total, message) =>
                {
                    ProgressCurrent = current;
                    ProgressTotal = total;
                    StatusMessage = message;
                });

            if (result.Success)
            {
                NewVehiclesFound = result.NewVehiclesFound;
                CurrentBatchId = result.BatchId;
                StatusMessage = $"✓ Discovery complete! Found {result.NewVehiclesFound} new vehicles";

                if (result.NewVehiclesFound > 0)
                {
                    await LoadPendingVehiclesAsync();
                }
            }
            else
            {
                StatusMessage = $"❌ Discovery failed: {result.ErrorMessage}";
                MessageBox.Show($"Discovery failed: {result.ErrorMessage}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error: {ex.Message}";
            MessageBox.Show($"Error during discovery: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsDiscovering = false;
        }
    }

    [RelayCommand]
    private async Task LoadPendingVehiclesAsync()
    {
        try
        {
            var vehicles = await _reviewService.LoadPendingVehiclesAsync(CurrentBatchId);

            PendingVehicles.Clear();
            foreach (var vehicle in vehicles)
            {
                PendingVehicles.Add(vehicle);
            }

            UpdateCounts();
            StatusMessage = $"Loaded {PendingVehicles.Count} pending vehicles";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error loading vehicles: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ApproveSelectedAsync()
    {
        var selected = PendingVehicles.Where(v => v.IsSelected).ToList();
        if (!selected.Any())
        {
            MessageBox.Show("Please select vehicles to approve", "No Selection",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var vehicleIds = selected.Select(v => v.PendingVehicleId).ToList();
            await _reviewService.ApproveBatchAsync(vehicleIds, "User");

            StatusMessage = $"✓ Approved {selected.Count} vehicles";
            await LoadPendingVehiclesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error approving vehicles: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task RejectSelectedAsync()
    {
        var selected = PendingVehicles.Where(v => v.IsSelected).ToList();
        if (!selected.Any())
        {
            MessageBox.Show("Please select vehicles to reject", "No Selection",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to reject {selected.Count} vehicles? They will be removed from the pending list.",
            "Confirm Rejection",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            var vehicleIds = selected.Select(v => v.PendingVehicleId).ToList();
            await _reviewService.RejectBatchAsync(vehicleIds, "User");

            StatusMessage = $"✓ Rejected {selected.Count} vehicles";
            await LoadPendingVehiclesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error rejecting vehicles: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ProcessApprovedVehiclesAsync()
    {
        var approvedCount = await _reviewService.GetPendingCountAsync();
        if (approvedCount == 0)
        {
            MessageBox.Show("No approved vehicles to process. Please approve vehicles first.",
                "No Approved Vehicles", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"This will process all approved vehicles and create:\n" +
            $"• Vehicle types in database\n" +
            $"• Consolidated models\n" +
            $"• Automatic couplings for overlapping models\n\n" +
            $"Continue?",
            "Confirm Processing",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            IsProcessing = true;
            StatusMessage = "Processing approved vehicles...";

            var processingResult = await _reviewService.ProcessApprovedVehiclesAsync(
                CurrentBatchId,
                progressCallback: (current, total, message) =>
                {
                    ProgressCurrent = current;
                    ProgressTotal = total;
                    StatusMessage = message;
                });

            if (processingResult.Success)
            {
                var summary = $"✓ Processing Complete!\n\n" +
                             $"Vehicles Created: {processingResult.VehicleTypesCreated}\n" +
                             $"Consolidated Models Created: {processingResult.ConsolidatedModelsCreated}\n" +
                             $"Auto-Couplings Created: {processingResult.CouplingsCreated}\n" +
                             $"Manufacturers Created: {processingResult.ManufacturersCreated}";

                if (processingResult.Errors.Any())
                {
                    summary += $"\n\nErrors ({processingResult.Errors.Count}):\n" +
                              string.Join("\n", processingResult.Errors.Take(5));
                }

                StatusMessage = $"✓ Created {processingResult.VehicleTypesCreated} vehicles, {processingResult.ConsolidatedModelsCreated} models, {processingResult.CouplingsCreated} couplings";

                MessageBox.Show(summary, "Processing Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadPendingVehiclesAsync();
            }
            else
            {
                MessageBox.Show($"Processing failed:\n\n{string.Join("\n", processingResult.Errors)}",
                    "Processing Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error processing vehicles: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var vehicle in PendingVehicles)
        {
            vehicle.IsSelected = true;
        }
        UpdateCounts();
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var vehicle in PendingVehicles)
        {
            vehicle.IsSelected = false;
        }
        UpdateCounts();
    }

    private void UpdateCounts()
    {
        ApprovedCount = PendingVehicles.Count(v => v.IsSelected);
        OnPropertyChanged(nameof(HasPendingVehicles));
    }

    partial void OnPendingVehiclesChanged(ObservableCollection<PendingVehicleDisplayModel> value)
    {
        UpdateCounts();
    }
}
