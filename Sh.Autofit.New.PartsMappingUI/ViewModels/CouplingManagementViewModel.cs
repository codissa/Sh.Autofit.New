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
    private readonly IDbContextFactory<ShAutofitContext> _contextFactory;

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

    // Recommendations
    [ObservableProperty]
    private ObservableCollection<CouplingRecommendation> _couplingRecommendations = new();

    [ObservableProperty]
    private bool _noRecommendations = true;

    [ObservableProperty]
    private ObservableCollection<string> _availableManufacturersForRecommendations = new();

    [ObservableProperty]
    private string? _selectedRecommendationManufacturer = "הכל";

    [ObservableProperty]
    private bool _isGeneratingRecommendations;

    private CancellationTokenSource? _recommendationsCancellation;

    public int RecommendationsCount => CouplingRecommendations?.Count ?? 0;

    public CouplingManagementViewModel(IDataService dataService, IDbContextFactory<ShAutofitContext> contextFactory)
    {
        _dataService = dataService;
        _contextFactory = contextFactory;
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
                var coupledModelId = coupling.ConsolidatedModelIdA == model.ConsolidatedModelId
                    ? coupling.ConsolidatedModelIdB
                    : coupling.ConsolidatedModelIdA;

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

        // Show dialog to select model(s) to couple with
        var availableModels = AllModels
            .Where(m => m.ConsolidatedModelId != SelectedModel.ConsolidatedModelId)
            .Where(m => !CoupledModels.Any(c => c.CoupledModel.ConsolidatedModelId == m.ConsolidatedModelId))
            .ToList();

        if (!availableModels.Any())
        {
            MessageBox.Show("אין דגמים זמינים לצימוד", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = CreateSelectionDialog("בחר דגם/דגמים לצימוד", availableModels, m =>
            $"{m.Manufacturer?.ManufacturerShortName} - {m.CommercialName} - {m.ModelName} - {m.EngineVolume} סמ\"ק - {m.TransmissionType} - {m.FuelTypeName} ({m.YearFrom}-{m.YearTo})");

        if (dialog.ShowDialog() == true && dialog.Tag is List<ConsolidatedVehicleModel> selectedModels)
        {
            try
            {
                IsLoading = true;
                StatusMessage = $"יוצר {selectedModels.Count} צימודים...";

                var successCount = 0;
                var errorMessages = new List<string>();

                foreach (var selectedModel in selectedModels)
                {
                    try
                    {
                        await _dataService.CreateModelCouplingAsync(
                            SelectedModel.ConsolidatedModelId,
                            selectedModel.ConsolidatedModelId,
                            "Compatible",
                            $"צימוד בין {SelectedModel.ModelName} ל-{selectedModel.ModelName}",
                            "current_user");

                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errorMessages.Add($"{selectedModel.ModelName}: {ex.Message}");
                    }
                }

                await LoadModelCouplingsAsync(SelectedModel);

                // Show summary message
                var summaryMessage = $"צומדו בהצלחה {successCount} מתוך {selectedModels.Count} דגמים";
                if (errorMessages.Any())
                {
                    summaryMessage += $"\n\nשגיאות:\n{string.Join("\n", errorMessages)}";
                    MessageBox.Show(summaryMessage, "סיום צימוד", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show(summaryMessage, "הצלחה", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה כללית ביצירת צימודים: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
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

    [RelayCommand]
    private async Task BreakModelCouplingAsync(ModelCouplingDisplayModel? coupling)
    {
        if (coupling == null || SelectedModel == null) return;

        // Get all parts mapped to the coupled model
        var coupledModelParts = await _dataService.LoadMappedPartsForConsolidatedModelAsync(
            coupling.CoupledModel.ConsolidatedModelId,
            includeCouplings: false); // Don't include inherited parts, only direct mappings

        var result = MessageBox.Show(
            $"⚡ שבירת צימוד עם {coupling.CoupledModel.ModelName}\n\n" +
            $"פעולה זו תבצע את השלבים הבאים:\n" +
            $"1. העתקת {coupledModelParts.Count} חלקים מהדגם המצומד אל '{SelectedModel.ModelName}'\n" +
            $"2. ביטול הצימוד בין הדגמים\n\n" +
            $"לאחר השבירה, כל דגם יהיה עצמאי וניתן יהיה לנהל את המיפויים שלו בנפרד.\n\n" +
            "האם להמשיך?",
            "אישור שבירת צימוד",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                IsLoading = true;
                StatusMessage = "שובר צימוד ומעתיק מיפויים...";

                // Step 1: Copy all parts from coupled model to selected model (if not already mapped)
                var currentlyMappedParts = await _dataService.LoadMappedPartsForConsolidatedModelAsync(
                    SelectedModel.ConsolidatedModelId,
                    includeCouplings: false);

                var currentPartNumbers = new HashSet<string>(currentlyMappedParts.Select(p => p.PartNumber));
                var partNumbersToCopy = coupledModelParts
                    .Where(p => !currentPartNumbers.Contains(p.PartNumber))
                    .Select(p => p.PartNumber)
                    .ToList();

                if (partNumbersToCopy.Any())
                {
                    StatusMessage = $"מעתיק {partNumbersToCopy.Count} חלקים...";
                    await _dataService.MapPartsToConsolidatedModelAsync(
                        SelectedModel.ConsolidatedModelId,
                        partNumbersToCopy,
                        "current_user");
                }

                // Step 2: Delete the coupling
                StatusMessage = "מבטל צימוד...";
                await _dataService.DeleteModelCouplingAsync(coupling.CouplingId);

                // Reload
                await LoadModelCouplingsAsync(SelectedModel);

                var message = $"✓ הצימוד נשבר בהצלחה!\n\n" +
                             $"הועתקו {partNumbersToCopy.Count} חלקים חדשים ל-'{SelectedModel.ModelName}'.\n" +
                             $"כעת הדגם עצמאי וניתן לנהל את המיפויים שלו בנפרד.";

                StatusMessage = "הצימוד נשבר בהצלחה";
                MessageBox.Show(message, "הצלחה", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"שגיאה: {ex.Message}";
                MessageBox.Show($"שגיאה בשבירת צימוד: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
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
                var coupledPartKey = coupling.PartItemKeyA == part.PartNumber
                    ? coupling.PartItemKeyB
                    : coupling.PartItemKeyA;

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

        // Show dialog to select part(s) to couple with
        var availableParts = AllParts
            .Where(p => p.PartNumber != SelectedPart.PartNumber)
            .Where(p => !CoupledParts.Any(c => c.CoupledPartKey == p.PartNumber))
            .ToList();

        if (!availableParts.Any())
        {
            MessageBox.Show("אין חלקים זמינים לצימוד", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = CreateSelectionDialog("בחר חלק/חלקים לצימוד", availableParts, p =>
            $"{p.PartNumber} - {p.PartName}");

        if (dialog.ShowDialog() == true && dialog.Tag is List<PartDisplayModel> selectedParts)
        {
            try
            {
                IsLoading = true;
                StatusMessage = $"יוצר {selectedParts.Count} צימודים...";

                var successCount = 0;
                var errorMessages = new List<string>();

                foreach (var selectedPart in selectedParts)
                {
                    try
                    {
                        await _dataService.CreatePartCouplingAsync(
                            SelectedPart.PartNumber,
                            selectedPart.PartNumber,
                            "Compatible",
                            $"צימוד בין {SelectedPart.PartNumber} ל-{selectedPart.PartNumber}",
                            "current_user");

                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errorMessages.Add($"{selectedPart.PartNumber}: {ex.Message}");
                    }
                }

                await LoadPartCouplingsAsync(SelectedPart);

                // Show summary message
                var summaryMessage = $"צומדו בהצלחה {successCount} מתוך {selectedParts.Count} חלקים";
                if (errorMessages.Any())
                {
                    summaryMessage += $"\n\nשגיאות:\n{string.Join("\n", errorMessages)}";
                    MessageBox.Show(summaryMessage, "סיום צימוד", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show(summaryMessage, "הצלחה", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה כללית ביצירת צימודים: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
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
            Width = 700,
            Height = 500,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            FlowDirection = FlowDirection.RightToLeft
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Instructions
        var instructionsText = new TextBlock
        {
            Text = "בחר דגם אחד או יותר לצימוד (החזק Ctrl לבחירה מרובה)",
            Margin = new Thickness(10, 10, 10, 5),
            FontSize = 12,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(102, 102, 102))
        };
        Grid.SetRow(instructionsText, 0);

        // Search box
        var searchBox = new TextBox { Margin = new Thickness(10, 5, 10, 10), Padding = new Thickness(8) };
        Grid.SetRow(searchBox, 1);

        var listBox = new ListBox
        {
            Margin = new Thickness(10, 0, 10, 0),
            SelectionMode = System.Windows.Controls.SelectionMode.Extended
        };
        var allItems = items.Select(i => new { Item = i, Display = displayFunc(i) }).ToList();
        listBox.ItemsSource = allItems;
        listBox.DisplayMemberPath = "Display";
        Grid.SetRow(listBox, 2);

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
            if (listBox.SelectedItems != null && listBox.SelectedItems.Count > 0)
            {
                var selectedItems = listBox.SelectedItems.Cast<dynamic>().Select(x => (T)x.Item).ToList();
                dialog.Tag = selectedItems;
                dialog.DialogResult = true;
                dialog.Close();
            }
        };

        var cancelButton = new Button { Content = "ביטול", Width = 100, Height = 30, Margin = new Thickness(5) };
        cancelButton.Click += (s, e) => dialog.Close();

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        Grid.SetRow(buttonPanel, 3);

        grid.Children.Add(instructionsText);
        grid.Children.Add(searchBox);
        grid.Children.Add(listBox);
        grid.Children.Add(buttonPanel);
        dialog.Content = grid;

        return dialog;
    }

    #endregion

    #region Recommendations

    [RelayCommand]
    private async Task RefreshRecommendationsAsync()
    {
        // Cancel any ongoing generation
        _recommendationsCancellation?.Cancel();
        _recommendationsCancellation = new CancellationTokenSource();
        var cancellationToken = _recommendationsCancellation.Token;

        try
        {
            IsGeneratingRecommendations = true;
            StatusMessage = "מחשב המלצות צימוד...";

            CouplingRecommendations.Clear();
            NoRecommendations = false;

            // Populate manufacturer filter
            var manufacturers = AllModels
                .Select(m => m.Manufacturer?.ManufacturerShortName ?? m.Manufacturer?.ManufacturerName ?? "לא ידוע")
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            AvailableManufacturersForRecommendations.Clear();
            AvailableManufacturersForRecommendations.Add("הכל");
            foreach (var mfg in manufacturers)
            {
                AvailableManufacturersForRecommendations.Add(mfg);
            }

            // Filter models by selected manufacturer if needed
            var modelsToProcess = AllModels.ToList();
            if (!string.IsNullOrEmpty(SelectedRecommendationManufacturer) && SelectedRecommendationManufacturer != "הכל")
            {
                modelsToProcess = modelsToProcess.Where(m =>
                    (m.Manufacturer?.ManufacturerShortName ?? m.Manufacturer?.ManufacturerName) == SelectedRecommendationManufacturer
                ).ToList();
            }

            StatusMessage = $"טוען צימודים קיימים...";

            // Get all existing couplings in ONE batch query - much faster
            var existingCouplings = new HashSet<(int, int)>();
            await using (var context = await _contextFactory.CreateDbContextAsync())
            {
                var allCouplings = await context.ModelCouplings
                    .Where(mc => mc.IsActive)
                    .Select(mc => new { mc.ConsolidatedModelIdA, mc.ConsolidatedModelIdB })
                    .ToListAsync(cancellationToken);

                foreach (var coupling in allCouplings)
                {
                    existingCouplings.Add((
                        Math.Min(coupling.ConsolidatedModelIdA, coupling.ConsolidatedModelIdB),
                        Math.Max(coupling.ConsolidatedModelIdA, coupling.ConsolidatedModelIdB)
                    ));
                }
            }

            StatusMessage = "מחשב התאמות...";

            // Process in batches to avoid UI freeze and allow cancellation
            var recommendations = new List<CouplingRecommendation>();
            int totalPairs = (modelsToProcess.Count * (modelsToProcess.Count - 1)) / 2;
            int processedPairs = 0;
            int batchSize = 100; // Process 100 pairs at a time
            int recommendationsFound = 0;

            for (int i = 0; i < modelsToProcess.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var modelA = modelsToProcess[i];

                for (int j = i + 1; j < modelsToProcess.Count; j++)
                {
                    var modelB = modelsToProcess[j];

                    // Skip if already coupled
                    var pairKey = (
                        Math.Min(modelA.ConsolidatedModelId, modelB.ConsolidatedModelId),
                        Math.Max(modelA.ConsolidatedModelId, modelB.ConsolidatedModelId)
                    );
                    if (existingCouplings.Contains(pairKey))
                        continue;

                    // Check if they're from the same manufacturer
                    var sameManufacturer = modelA.ManufacturerId == modelB.ManufacturerId ||
                                          (modelA.Manufacturer?.ManufacturerShortName?.Equals(
                                              modelB.Manufacturer?.ManufacturerShortName,
                                              StringComparison.OrdinalIgnoreCase) ?? false);

                    if (!sameManufacturer)
                        continue;

                    var recommendation = CalculateModelSimilarity(modelA, modelB);
                    if (recommendation != null)
                    {
                        recommendations.Add(recommendation);
                        recommendationsFound++;
                    }

                    processedPairs++;

                    // Update UI every batch and allow UI to breathe
                    if (processedPairs % batchSize == 0)
                    {
                        var progress = (processedPairs * 100) / totalPairs;
                        StatusMessage = $"מחשב התאמות... {progress}% ({recommendationsFound} נמצאו)";

                        // Add recommendations to UI in batches (sorted by score)
                        foreach (var rec in recommendations.OrderByDescending(r => r.MatchScore).Take(10))
                        {
                            if (!CouplingRecommendations.Contains(rec))
                            {
                                CouplingRecommendations.Add(rec);
                            }
                        }
                        OnPropertyChanged(nameof(RecommendationsCount));

                        // Allow UI thread to process
                        await Task.Delay(1, cancellationToken);
                    }
                }
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                // Add all remaining recommendations
                foreach (var rec in recommendations.OrderByDescending(r => r.MatchScore))
                {
                    if (!CouplingRecommendations.Contains(rec))
                    {
                        CouplingRecommendations.Add(rec);
                    }
                }

                NoRecommendations = !CouplingRecommendations.Any();
                OnPropertyChanged(nameof(RecommendationsCount));

                StatusMessage = $"נמצאו {CouplingRecommendations.Count} המלצות צימוד";
            }
            else
            {
                StatusMessage = "החישוב בוטל";
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "החישוב בוטל";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"שגיאה בחישוב המלצות: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsGeneratingRecommendations = false;
        }
    }

    [RelayCommand]
    private void CancelRecommendations()
    {
        _recommendationsCancellation?.Cancel();
        StatusMessage = "מבטל...";
    }

    partial void OnSelectedRecommendationManufacturerChanged(string? value)
    {
        // Auto-refresh when manufacturer filter changes
        _ = RefreshRecommendationsAsync();
    }

    private CouplingRecommendation? CalculateModelSimilarity(ConsolidatedVehicleModel modelA, ConsolidatedVehicleModel modelB)
    {
        int score = 0;
        var reasons = new List<string>();

        // Same model name (highest priority)
        if (modelA.ModelName?.Equals(modelB.ModelName, StringComparison.OrdinalIgnoreCase) == true)
        {
            score += 50;
            reasons.Add("אותו שם דגם");
        }

        // Overlapping years
        var yearsOverlap = !(modelA.YearTo < modelB.YearFrom || modelB.YearTo < modelA.YearFrom);
        if (yearsOverlap)
        {
            score += 30;
            reasons.Add("שנים חופפות");
        }

        // Same engine volume
        if (modelA.EngineVolume.HasValue && modelB.EngineVolume.HasValue &&
            modelA.EngineVolume.Value == modelB.EngineVolume.Value)
        {
            score += 15;
            reasons.Add("נפח מנוע זהה");
        }

        // Same transmission type
        if (!string.IsNullOrEmpty(modelA.TransmissionType) &&
            !string.IsNullOrEmpty(modelB.TransmissionType) &&
            modelA.TransmissionType.Equals(modelB.TransmissionType, StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
            reasons.Add("תיבת הילוכים זהה");
        }

        // Same fuel type
        if (!string.IsNullOrEmpty(modelA.FuelTypeName) &&
            !string.IsNullOrEmpty(modelB.FuelTypeName) &&
            modelA.FuelTypeName.Equals(modelB.FuelTypeName, StringComparison.OrdinalIgnoreCase))
        {
            score += 5;
            reasons.Add("סוג דלק זהה");
        }

        // Only recommend if score is high enough (at least model name match or years + engine)
        if (score >= 45)
        {
            return new CouplingRecommendation
            {
                ModelA = modelA,
                ModelB = modelB,
                MatchScore = score,
                MatchReason = string.Join(", ", reasons),
                IsSelected = false
            };
        }

        return null;
    }

    [RelayCommand]
    private async Task ApproveRecommendationAsync(CouplingRecommendation? recommendation)
    {
        if (recommendation == null)
            return;

        try
        {
            IsLoading = true;
            StatusMessage = $"יוצר צימוד...";

            await _dataService.CreateModelCouplingAsync(
                recommendation.ModelA.ConsolidatedModelId,
                recommendation.ModelB.ConsolidatedModelId,
                "Compatible",
                $"צימוד אוטומטי: {recommendation.MatchReason}",
                "current_user");

            CouplingRecommendations.Remove(recommendation);
            NoRecommendations = !CouplingRecommendations.Any();
            OnPropertyChanged(nameof(RecommendationsCount));

            StatusMessage = $"✓ צימוד נוצר בהצלחה בין {recommendation.ModelA.ModelName} ל-{recommendation.ModelB.ModelName}";
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

    [RelayCommand]
    private void RejectRecommendation(CouplingRecommendation? recommendation)
    {
        if (recommendation == null)
            return;

        CouplingRecommendations.Remove(recommendation);
        NoRecommendations = !CouplingRecommendations.Any();
        OnPropertyChanged(nameof(RecommendationsCount));
    }

    [RelayCommand]
    private async Task ApproveAllRecommendationsAsync()
    {
        var selectedRecs = CouplingRecommendations.Where(r => r.IsSelected).ToList();
        if (!selectedRecs.Any())
        {
            MessageBox.Show("אנא בחר לפחות המלצה אחת", "מידע", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"האם ליצור {selectedRecs.Count} צימודים?",
            "אישור",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            IsLoading = true;
            var successCount = 0;
            var errorMessages = new List<string>();

            foreach (var rec in selectedRecs)
            {
                try
                {
                    StatusMessage = $"יוצר צימוד {successCount + 1}/{selectedRecs.Count}...";

                    await _dataService.CreateModelCouplingAsync(
                        rec.ModelA.ConsolidatedModelId,
                        rec.ModelB.ConsolidatedModelId,
                        "Compatible",
                        $"צימוד אוטומטי: {rec.MatchReason}",
                        "current_user");

                    CouplingRecommendations.Remove(rec);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errorMessages.Add($"{rec.ModelA.ModelName} ↔ {rec.ModelB.ModelName}: {ex.Message}");
                }
            }

            NoRecommendations = !CouplingRecommendations.Any();
            OnPropertyChanged(nameof(RecommendationsCount));

            var summaryMessage = $"נוצרו {successCount} צימודים בהצלחה";
            if (errorMessages.Any())
            {
                summaryMessage += $"\n\nשגיאות:\n{string.Join("\n", errorMessages)}";
                MessageBox.Show(summaryMessage, "סיום", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show(summaryMessage, "הצלחה", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            StatusMessage = summaryMessage;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"שגיאה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void RejectAllRecommendations()
    {
        var selectedRecs = CouplingRecommendations.Where(r => r.IsSelected).ToList();
        if (!selectedRecs.Any())
        {
            MessageBox.Show("אנא בחר לפחות המלצה אחת", "מידע", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"האם למחוק {selectedRecs.Count} המלצות?",
            "אישור",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        foreach (var rec in selectedRecs.ToList())
        {
            CouplingRecommendations.Remove(rec);
        }

        NoRecommendations = !CouplingRecommendations.Any();
        OnPropertyChanged(nameof(RecommendationsCount));
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

public partial class CouplingRecommendation : ObservableObject
{
    public ConsolidatedVehicleModel ModelA { get; set; } = null!;
    public ConsolidatedVehicleModel ModelB { get; set; } = null!;
    public int MatchScore { get; set; }
    public string MatchReason { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}
