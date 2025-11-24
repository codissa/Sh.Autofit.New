using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Models;
using Sh.Autofit.New.PartsMappingUI.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Sh.Autofit.New.PartsMappingUI.ViewModels;

public partial class CouplingManagementViewModel : ObservableObject
{
    private readonly IDataService _dataService;

    // Model Coupling
    [ObservableProperty]
    private ObservableCollection<ConsolidatedVehicleModel> _allModels = new();

    [ObservableProperty]
    private ObservableCollection<ConsolidatedVehicleModel> _filteredModels = new();

    [ObservableProperty]
    private ConsolidatedVehicleModel? _selectedModel;

    [ObservableProperty]
    private ObservableCollection<ModelCouplingDisplayModel> _coupledModels = new();

    [ObservableProperty]
    private string _modelSearchText = string.Empty;

    [ObservableProperty]
    private string _modelCouplingStatus = string.Empty;

    [ObservableProperty]
    private bool _noCoupledModels = true;

    // Part Coupling
    [ObservableProperty]
    private ObservableCollection<PartDisplayModel> _allParts = new();

    [ObservableProperty]
    private ObservableCollection<PartDisplayModel> _filteredParts = new();

    [ObservableProperty]
    private PartDisplayModel? _selectedPart;

    [ObservableProperty]
    private ObservableCollection<PartCouplingDisplayModel> _coupledParts = new();

    [ObservableProperty]
    private string _partSearchText = string.Empty;

    [ObservableProperty]
    private string _partCouplingStatus = string.Empty;

    [ObservableProperty]
    private bool _noCoupledParts = true;

    // Common
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public CouplingManagementViewModel(IDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task InitializeAsync()
    {
        await LoadModelsAsync();
        await LoadPartsAsync();
    }

    #region Model Coupling

    private async Task LoadModelsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "טוען דגמים...";

            var models = await _dataService.LoadConsolidatedModelsAsync();
            AllModels.Clear();
            foreach (var model in models.OrderBy(m => m.Manufacturer?.ManufacturerShortName)
                                         .ThenBy(m => m.ModelName))
            {
                AllModels.Add(model);
            }

            ApplyModelFilter();
            StatusMessage = $"נטענו {AllModels.Count} דגמים";
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnModelSearchTextChanged(string value)
    {
        ApplyModelFilter();
    }

    private void ApplyModelFilter()
    {
        var filtered = AllModels.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(ModelSearchText))
        {
            var searchLower = ModelSearchText.ToLower();
            filtered = filtered.Where(m =>
                (m.Manufacturer?.ManufacturerName?.ToLower().Contains(searchLower) ?? false) ||
                (m.Manufacturer?.ManufacturerShortName?.ToLower().Contains(searchLower) ?? false) ||
                (m.ModelName?.ToLower().Contains(searchLower) ?? false));
        }

        FilteredModels = new ObservableCollection<ConsolidatedVehicleModel>(filtered.Take(100));
    }

    partial void OnSelectedModelChanged(ConsolidatedVehicleModel? value)
    {
        if (value != null)
        {
            _ = LoadModelCouplingsAsync(value);
        }
        else
        {
            CoupledModels.Clear();
            NoCoupledModels = true;
        }
    }

    private async Task LoadModelCouplingsAsync(ConsolidatedVehicleModel model)
    {
        try
        {
            IsLoading = true;
            ModelCouplingStatus = "טוען צימודים...";

            var couplings = await _dataService.GetModelCouplingsAsync(model.ConsolidatedModelId);

            CoupledModels.Clear();
            foreach (var coupling in couplings)
            {
                // Get the "other" model in the coupling
                var coupledModelId = coupling.ConsolidatedModelId_A == model.ConsolidatedModelId
                    ? coupling.ConsolidatedModelId_B
                    : coupling.ConsolidatedModelId_A;

                var coupledModel = await _dataService.GetConsolidatedModelByIdAsync(coupledModelId);
                if (coupledModel != null)
                {
                    CoupledModels.Add(new ModelCouplingDisplayModel
                    {
                        CouplingId = coupling.ModelCouplingId,
                        CoupledModel = coupledModel,
                        CouplingType = coupling.CouplingType ?? "Compatible",
                        Notes = coupling.Notes ?? ""
                    });
                }
            }

            NoCoupledModels = !CoupledModels.Any();
            ModelCouplingStatus = CoupledModels.Any()
                ? $"{CoupledModels.Count} דגמים מצומדים"
                : "";
        }
        catch (Exception ex)
        {
            ModelCouplingStatus = $"שגיאה: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddModelCouplingAsync()
    {
        if (SelectedModel == null)
        {
            MessageBox.Show("אנא בחר דגם תחילה", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Show dialog to select model to couple with
        var availableModels = AllModels
            .Where(m => m.ConsolidatedModelId != SelectedModel.ConsolidatedModelId)
            .Where(m => !CoupledModels.Any(c => c.CoupledModel.ConsolidatedModelId == m.ConsolidatedModelId))
            .ToList();

        if (!availableModels.Any())
        {
            MessageBox.Show("אין דגמים זמינים לצימוד", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = CreateSelectionDialog("בחר דגם לצימוד", availableModels, m =>
            $"{m.Manufacturer?.ManufacturerShortName} - {m.CommercialName}  - {m.ModelName} - {m.EngineVolume}- {m.TransmissionType} - {m.FuelTypeName} ({m.YearFrom}-{m.YearTo})");

        if (dialog.ShowDialog() == true && dialog.Tag is ConsolidatedVehicleModel selectedModel)
        {
            try
            {
                IsLoading = true;
                StatusMessage = "יוצר צימוד...";

                await _dataService.CreateModelCouplingAsync(
                    SelectedModel.ConsolidatedModelId,
                    selectedModel.ConsolidatedModelId,
                    "Compatible",
                    $"צימוד בין {SelectedModel.ModelName} ל-{selectedModel.ModelName}",
                    "current_user");

                await LoadModelCouplingsAsync(SelectedModel);

                MessageBox.Show(
                    $"הדגם {selectedModel.ModelName} צומד בהצלחה ל-{SelectedModel.ModelName}",
                    "הצלחה",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה ביצירת צימוד: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task RemoveModelCouplingAsync(ModelCouplingDisplayModel? coupling)
    {
        if (coupling == null || SelectedModel == null) return;

        var result = MessageBox.Show(
            $"האם לבטל את הצימוד עם {coupling.CoupledModel.ModelName}?",
            "אישור ביטול צימוד",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                IsLoading = true;
                StatusMessage = "מבטל צימוד...";

                await _dataService.DeleteModelCouplingAsync(coupling.CouplingId);
                await LoadModelCouplingsAsync(SelectedModel);

                StatusMessage = "הצימוד בוטל בהצלחה";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בביטול צימוד: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    #endregion

    #region Part Coupling

    private async Task LoadPartsAsync()
    {
        try
        {
            var parts = await _dataService.LoadPartsAsync();
            AllParts.Clear();
            foreach (var part in parts.OrderBy(p => p.PartNumber))
            {
                AllParts.Add(part);
            }

            ApplyPartFilter();
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה בטעינת חלקים: {ex.Message}";
        }
    }

    partial void OnPartSearchTextChanged(string value)
    {
        ApplyPartFilter();
    }

    private void ApplyPartFilter()
    {
        var filtered = AllParts.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(PartSearchText))
        {
            var searchLower = PartSearchText.ToLower();
            filtered = filtered.Where(p =>
                (p.PartNumber?.ToLower().Contains(searchLower) ?? false) ||
                (p.PartName?.ToLower().Contains(searchLower) ?? false) ||
                (p.Category?.ToLower().Contains(searchLower) ?? false));
        }

        FilteredParts = new ObservableCollection<PartDisplayModel>(filtered.Take(100));
    }

    partial void OnSelectedPartChanged(PartDisplayModel? value)
    {
        if (value != null)
        {
            _ = LoadPartCouplingsAsync(value);
        }
        else
        {
            CoupledParts.Clear();
            NoCoupledParts = true;
        }
    }

    private async Task LoadPartCouplingsAsync(PartDisplayModel part)
    {
        try
        {
            IsLoading = true;
            PartCouplingStatus = "טוען צימודים...";

            var couplings = await _dataService.GetPartCouplingsAsync(part.PartNumber);

            CoupledParts.Clear();
            foreach (var coupling in couplings)
            {
                // Get the "other" part in the coupling
                var coupledPartKey = coupling.PartItemKey_A == part.PartNumber
                    ? coupling.PartItemKey_B
                    : coupling.PartItemKey_A;

                // Find part info
                var coupledPart = AllParts.FirstOrDefault(p => p.PartNumber == coupledPartKey);

                CoupledParts.Add(new PartCouplingDisplayModel
                {
                    CouplingId = coupling.PartCouplingId,
                    CoupledPartKey = coupledPartKey ?? "",
                    CoupledPartName = coupledPart?.PartName ?? "לא ידוע",
                    CouplingType = coupling.CouplingType ?? "Compatible",
                    Notes = coupling.Notes ?? ""
                });
            }

            NoCoupledParts = !CoupledParts.Any();
            PartCouplingStatus = CoupledParts.Any()
                ? $"{CoupledParts.Count} חלקים מצומדים"
                : "";
        }
        catch (Exception ex)
        {
            PartCouplingStatus = $"שגיאה: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddPartCouplingAsync()
    {
        if (SelectedPart == null)
        {
            MessageBox.Show("אנא בחר חלק תחילה", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Show dialog to select part to couple with
        var availableParts = AllParts
            .Where(p => p.PartNumber != SelectedPart.PartNumber)
            .Where(p => !CoupledParts.Any(c => c.CoupledPartKey == p.PartNumber))
            .ToList();

        if (!availableParts.Any())
        {
            MessageBox.Show("אין חלקים זמינים לצימוד", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = CreateSelectionDialog("בחר חלק לצימוד", availableParts, p =>
            $"{p.PartNumber} - {p.PartName}");

        if (dialog.ShowDialog() == true && dialog.Tag is PartDisplayModel selectedPart)
        {
            try
            {
                IsLoading = true;
                StatusMessage = "יוצר צימוד...";

                await _dataService.CreatePartCouplingAsync(
                    SelectedPart.PartNumber,
                    selectedPart.PartNumber,
                    "Compatible",
                    $"צימוד בין {SelectedPart.PartNumber} ל-{selectedPart.PartNumber}",
                    "current_user");

                await LoadPartCouplingsAsync(SelectedPart);

                MessageBox.Show(
                    $"החלק {selectedPart.PartNumber} צומד בהצלחה ל-{SelectedPart.PartNumber}",
                    "הצלחה",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה ביצירת צימוד: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task RemovePartCouplingAsync(PartCouplingDisplayModel? coupling)
    {
        if (coupling == null || SelectedPart == null) return;

        var result = MessageBox.Show(
            $"האם לבטל את הצימוד עם {coupling.CoupledPartKey}?",
            "אישור ביטול צימוד",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                IsLoading = true;
                StatusMessage = "מבטל צימוד...";

                await _dataService.DeletePartCouplingAsync(coupling.CouplingId);
                await LoadPartCouplingsAsync(SelectedPart);

                StatusMessage = "הצימוד בוטל בהצלחה";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בביטול צימוד: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    #endregion

    #region Helpers

    private Window CreateSelectionDialog<T>(string title, List<T> items, Func<T, string> displayFunc)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 600,
            Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            FlowDirection = FlowDirection.RightToLeft
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Search box
        var searchBox = new TextBox { Margin = new Thickness(10), Padding = new Thickness(8) };
        Grid.SetRow(searchBox, 0);

        var listBox = new ListBox { Margin = new Thickness(10, 0, 10, 0) };
        var allItems = items.Select(i => new { Item = i, Display = displayFunc(i) }).ToList();
        listBox.ItemsSource = allItems;
        listBox.DisplayMemberPath = "Display";
        Grid.SetRow(listBox, 1);

        searchBox.TextChanged += (s, e) =>
        {
            var search = searchBox.Text?.ToLower() ?? "";
            listBox.ItemsSource = string.IsNullOrWhiteSpace(search)
                ? allItems
                : allItems.Where(x => x.Display.ToLower().Contains(search)).ToList();
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(10)
        };

        var okButton = new Button { Content = "אישור", Width = 100, Height = 30, Margin = new Thickness(5) };
        okButton.Click += (s, e) =>
        {
            if (listBox.SelectedItem != null)
            {
                var selected = (dynamic)listBox.SelectedItem;
                dialog.Tag = selected.Item;
                dialog.DialogResult = true;
                dialog.Close();
            }
        };

        var cancelButton = new Button { Content = "ביטול", Width = 100, Height = 30, Margin = new Thickness(5) };
        cancelButton.Click += (s, e) => dialog.Close();

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        Grid.SetRow(buttonPanel, 2);

        grid.Children.Add(searchBox);
        grid.Children.Add(listBox);
        grid.Children.Add(buttonPanel);
        dialog.Content = grid;

        return dialog;
    }

    #endregion
}

public class ModelCouplingDisplayModel
{
    public int CouplingId { get; set; }
    public ConsolidatedVehicleModel CoupledModel { get; set; } = null!;
    public string CouplingType { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public class PartCouplingDisplayModel
{
    public int CouplingId { get; set; }
    public string CoupledPartKey { get; set; } = string.Empty;
    public string CoupledPartName { get; set; } = string.Empty;
    public string CouplingType { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
