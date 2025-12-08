using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Helpers;
using Sh.Autofit.New.PartsMappingUI.Models;
using Sh.Autofit.New.PartsMappingUI.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Sh.Autofit.New.PartsMappingUI.ViewModels;

public partial class ModelMappingsManagementViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly IDbContextFactory<ShAutofitContext> _contextFactory;

    // Consolidated models (NEW WAY - primary)
    [ObservableProperty]
    private ObservableCollection<ConsolidatedVehicleModel> _consolidatedModels = new();

    [ObservableProperty]
    private ObservableCollection<ConsolidatedVehicleModel> _filteredConsolidatedModels = new();

    [ObservableProperty]
    private ConsolidatedVehicleModel? _selectedConsolidatedModel;

    [ObservableProperty]
    private ObservableCollection<PartDisplayModel> _mappedParts = new();

    [ObservableProperty]
    private ObservableCollection<PartDisplayModel> _suggestedParts = new();

    [ObservableProperty]
    private string _modelSearchText = string.Empty;

    [ObservableProperty]
    private string? _selectedManufacturer;

    [ObservableProperty]
    private ObservableCollection<string> _availableManufacturers = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ModelMappingsManagementViewModel(IDataService dataService, IDbContextFactory<ShAutofitContext> contextFactory)
    {
        _dataService = dataService;
        _contextFactory = contextFactory;
    }

    public async Task InitializeAsync()
    {
        await LoadConsolidatedModelsAsync();
    }

    [RelayCommand]
    private async Task LoadConsolidatedModelsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "טוען דגמים מאוחדים...";

            // Load consolidated models directly from database (NEW WAY)
            var models = await _dataService.LoadConsolidatedModelsAsync();

            ConsolidatedModels.Clear();
            foreach (var model in models.OrderBy(m => m.Manufacturer?.ManufacturerShortName ?? m.Manufacturer?.ManufacturerName)
                                         .ThenBy(m => m.ModelName)
                                         .ThenBy(m => m.YearFrom))
            {
                ConsolidatedModels.Add(model);
            }

            // Extract unique manufacturers
            var manufacturers = models
                .Select(m => m.Manufacturer?.ManufacturerShortName ?? m.Manufacturer?.ManufacturerName ?? "לא ידוע")
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            AvailableManufacturers.Clear();
            AvailableManufacturers.Add("הכל"); // All
            foreach (var mfg in manufacturers)
            {
                AvailableManufacturers.Add(mfg);
            }

            StatusMessage = $"נטענו {ConsolidatedModels.Count} דגמים מאוחדים";
            ApplyFilters();
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה: {ex.Message}";
            MessageBox.Show($"שגיאה בטעינת דגמים: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnModelSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedManufacturerChanged(string? value)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = ConsolidatedModels.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(ModelSearchText))
        {
            var searchLower = ModelSearchText.ToLower();
            filtered = filtered.Where(m =>
                (m.Manufacturer?.ManufacturerName?.ToLower().Contains(searchLower) ?? false) ||
                (m.Manufacturer?.ManufacturerShortName?.ToLower().Contains(searchLower) ?? false) ||
                (m.ModelName?.ToLower().Contains(searchLower) ?? false) ||
                (m.CommercialName?.ToLower().Contains(searchLower) ?? false));
        }

        // Apply manufacturer filter
        if (!string.IsNullOrEmpty(SelectedManufacturer) && SelectedManufacturer != "הכל")
        {
            filtered = filtered.Where(m =>
                (m.Manufacturer?.ManufacturerShortName ?? m.Manufacturer?.ManufacturerName) == SelectedManufacturer);
        }

        FilteredConsolidatedModels = new ObservableCollection<ConsolidatedVehicleModel>(filtered);
    }

    partial void OnSelectedConsolidatedModelChanged(ConsolidatedVehicleModel? value)
    {
        if (value != null)
        {
            _ = LoadMappedPartsForConsolidatedModelAsync(value);
            _ = LoadSuggestedPartsForConsolidatedModelAsync(value);
        }
        else
        {
            MappedParts.Clear();
            SuggestedParts.Clear();
        }
    }

    private async Task LoadMappedPartsForConsolidatedModelAsync(ConsolidatedVehicleModel model)
    {
        try
        {
            IsLoading = true;
            StatusMessage = $"טוען חלקים ממודל מאוחד {model.ModelName}...";

            var mappedPartsList = await _dataService.LoadMappedPartsForConsolidatedModelAsync(
                model.ConsolidatedModelId,
                includeCouplings: true);

            MappedParts.Clear();
            foreach (var part in mappedPartsList.OrderBy(p => p.PartNumber))
            {
                MappedParts.Add(part);
            }

            var yearRange = model.YearTo.HasValue
                ? $"{model.YearFrom}-{model.YearTo}"
                : $"{model.YearFrom}+";

            if (MappedParts.Count == 0)
            {
                StatusMessage = $"אין חלקים ממופים לדגם {model.ModelName} ({yearRange}). לחץ על 'הוסף חלקים' כדי להתחיל.";
            }
            else
            {
                StatusMessage = $"{MappedParts.Count} חלקים ממופים לדגם {model.ModelName} ({yearRange})";
            }
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

    private async Task LoadSuggestedPartsForConsolidatedModelAsync(ConsolidatedVehicleModel model)
    {
        try
        {
            var manufacturerName = model.Manufacturer?.ManufacturerName ?? "";
            var suggestions = await _dataService.GetSuggestedPartsForModelAsync(
                manufacturerName,
                model.ModelName ?? "");

            SuggestedParts.Clear();
            foreach (var part in suggestions)
            {
                SuggestedParts.Add(part);
            }
        }
        catch (Exception)
        {
            // Silently fail for suggestions - they're optional
            SuggestedParts.Clear();
        }
    }

    [RelayCommand]
    private async Task AddPartsToModelAsync()
    {
        if (SelectedConsolidatedModel == null)
        {
            MessageBox.Show("אנא בחר דגם", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Use the old simple SelectPartsDialog
        var allParts = await _dataService.LoadPartsAsync();
        var unmappedParts = allParts.Where(p => !MappedParts.Any(mp => mp.PartNumber == p.PartNumber)).ToList();

        var dialog = new Views.SelectPartsDialog(unmappedParts);
        if (dialog.ShowDialog() == true && dialog.SelectedParts.Any())
        {
            try
            {
                IsLoading = true;
                var partNumbers = dialog.SelectedParts.Select(p => p.PartNumber).ToList();

                StatusMessage = $"ממפה {partNumbers.Count} חלקים למודל מאוחד...";
                await _dataService.MapPartsToConsolidatedModelAsync(
                    SelectedConsolidatedModel.ConsolidatedModelId,
                    partNumbers,
                    "current_user");

                var yearRange = SelectedConsolidatedModel.YearTo.HasValue
                    ? $"{SelectedConsolidatedModel.YearFrom}-{SelectedConsolidatedModel.YearTo}"
                    : $"{SelectedConsolidatedModel.YearFrom}+";
                StatusMessage = $"✓ מופו {partNumbers.Count} חלקים למודל {SelectedConsolidatedModel.ModelName} ({yearRange})";

                await LoadMappedPartsForConsolidatedModelAsync(SelectedConsolidatedModel);
                await LoadSuggestedPartsForConsolidatedModelAsync(SelectedConsolidatedModel);
            }
            catch (Exception ex)
            {
                StatusMessage = $"שגיאה: {ex.Message}";
                MessageBox.Show($"שגיאה במיפוי: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task RemovePartFromModelAsync(PartDisplayModel? part)
    {
        if (part == null || SelectedConsolidatedModel == null)
            return;

        // Block removal of parts that come from part couplings only
        if (part.MappingType == "CoupledPart")
        {
            MessageBox.Show(
                $"החלק '{part.PartName}' ממופה דרך צימוד חלקים.\n\n" +
                "לא ניתן להסיר את המיפוי מכאן.\n" +
                "יש להיכנס לניהול צימודים ולהסיר את הצימוד בין החלקים.",
                "לא ניתן להסיר מיפוי",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var yearRange = SelectedConsolidatedModel.YearTo.HasValue
            ? $"{SelectedConsolidatedModel.YearFrom}-{SelectedConsolidatedModel.YearTo}"
            : $"{SelectedConsolidatedModel.YearFrom}+";

        // Check if this model has active couplings
        var couplings = await _dataService.GetModelCouplingsAsync(SelectedConsolidatedModel.ConsolidatedModelId);
        var activeCouplings = couplings.Where(c => c.IsActive).ToList();

        // Collect ALL models that will be affected (for parts inherited from coupled models)
        var allAffectedModels = new List<int> { SelectedConsolidatedModel.ConsolidatedModelId };
        var coupledModelNames = new List<string>();

        foreach (var coupling in activeCouplings)
        {
            var otherModelId = coupling.ConsolidatedModelIdA == SelectedConsolidatedModel.ConsolidatedModelId
                ? coupling.ConsolidatedModelIdB
                : coupling.ConsolidatedModelIdA;

            allAffectedModels.Add(otherModelId);

            var otherModel = await _dataService.GetConsolidatedModelByIdAsync(otherModelId);
            if (otherModel != null)
            {
                coupledModelNames.Add($"{otherModel.Manufacturer?.ManufacturerShortName} {otherModel.ModelName}");
            }
        }

        string message;
        MessageBoxImage messageIcon;

        if (part.MappingType == "CoupledModel")
        {
            // Part comes from a coupled model - need to find which one and unmap from all
            var coupledModelsText = string.Join(", ", coupledModelNames);

            message = $"⚠️ החלק '{part.PartName}' ממופה דרך דגם מצומד!\n\n" +
                     $"הדגם '{SelectedConsolidatedModel.Manufacturer?.ManufacturerShortName} {SelectedConsolidatedModel.ModelName}' מצומד עם:\n{coupledModelsText}\n\n" +
                     $"האם להסיר את החלק מכל הדגמים המצומדים?\n\n" +
                     "לחץ 'כן' להסרה מכולם (כולל מהדגם שממנו מגיע המיפוי), 'לא' לביטול.\n\n" +
                     "💡 טיפ: אם ברצונך להסיר את הצימוד ולנהל כל דגם בנפרד, השתמש בכפתור 'שבור צימוד' בניהול צימודים.";
            messageIcon = MessageBoxImage.Warning;
        }
        else if (activeCouplings.Any())
        {
            // Part is directly mapped to this model, but model has couplings
            var coupledModelsText = string.Join(", ", coupledModelNames);

            message = $"הדגם '{SelectedConsolidatedModel.Manufacturer?.ManufacturerShortName} {SelectedConsolidatedModel.ModelName}' מצומד עם:\n{coupledModelsText}\n\n" +
                     $"האם להסיר את החלק '{part.PartName}' מדגם זה ומכל הדגמים המצומדים אליו?\n\n" +
                     "לחץ 'כן' להסרה מכולם, 'לא' לביטול.\n\n" +
                     "💡 טיפ: אם ברצונך להסיר את הצימוד ולנהל כל דגם בנפרד, השתמש בכפתור 'שבור צימוד' בניהול צימודים.";
            messageIcon = MessageBoxImage.Warning;
        }
        else
        {
            message = $"האם להסיר את '{part.PartName}' ממודל '{SelectedConsolidatedModel.ModelName}' ({yearRange})?";
            messageIcon = MessageBoxImage.Question;
        }

        var result = MessageBox.Show(
            message,
            "אישור הסרה",
            MessageBoxButton.YesNo,
            messageIcon
        );

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                IsLoading = true;

                if (part.MappingType == "CoupledModel")
                {
                    // Part is inherited from a coupled model - need to find which models have the direct mapping
                    StatusMessage = "מזהה מיפויים ישירים...";

                    // Check which of the coupled models actually have the direct mapping
                    var modelsWithDirectMapping = new List<int>();

                    foreach (var modelId in allAffectedModels)
                    {
                        // Load parts for this model WITHOUT coupling inheritance to see direct mappings only
                        var directParts = await _dataService.LoadMappedPartsForConsolidatedModelAsync(
                            modelId,
                            includeCouplings: false);

                        if (directParts.Any(p => p.PartNumber == part.PartNumber))
                        {
                            modelsWithDirectMapping.Add(modelId);
                        }
                    }

                    if (modelsWithDirectMapping.Any())
                    {
                        StatusMessage = "מסיר מיפוי מכל הדגמים המצומדים...";

                        // Unmap from all models that have the direct mapping
                        foreach (var modelId in modelsWithDirectMapping)
                        {
                            await _dataService.UnmapPartsFromConsolidatedModelAsync(
                                modelId,
                                new List<string> { part.PartNumber },
                                "current_user");
                        }

                        StatusMessage = $"החלק הוסר מ-{modelsWithDirectMapping.Count} דגמים בהצלחה (כולל כל הדגמים המצומדים)";
                    }
                    else
                    {
                        StatusMessage = "לא נמצאו מיפויים ישירים להסרה";
                        MessageBox.Show(
                            "לא נמצא מיפוי ישיר לחלק זה באף אחד מהדגמים המצומדים.\n" +
                            "ייתכן שהמיפוי כבר הוסר.",
                            "שגיאה",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
                else if (activeCouplings.Any())
                {
                    // Part is directly mapped to this model, and model has couplings
                    // Unmap from ALL affected models (this model + all coupled models)
                    StatusMessage = "מסיר מיפוי מכל הדגמים המצומדים...";

                    foreach (var modelId in allAffectedModels)
                    {
                        await _dataService.UnmapPartsFromConsolidatedModelAsync(
                            modelId,
                            new List<string> { part.PartNumber },
                            "current_user");
                    }

                    StatusMessage = $"החלק הוסר מ-{allAffectedModels.Count} דגמים מצומדים בהצלחה";
                }
                else
                {
                    // No couplings - simple unmap
                    StatusMessage = "מסיר חלק ממודל מאוחד...";

                    await _dataService.UnmapPartsFromConsolidatedModelAsync(
                        SelectedConsolidatedModel.ConsolidatedModelId,
                        new List<string> { part.PartNumber },
                        "current_user"
                    );
                    StatusMessage = "החלק הוסר ממודל מאוחד בהצלחה";
                }

                await LoadMappedPartsForConsolidatedModelAsync(SelectedConsolidatedModel);
                await LoadSuggestedPartsForConsolidatedModelAsync(SelectedConsolidatedModel);
            }
            catch (Exception ex)
            {
                StatusMessage = $"שגיאה: {ex.Message}";
                MessageBox.Show($"שגיאה בהסרת חלק: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task CopyMappingFromOtherModelAsync()
    {
        if (SelectedConsolidatedModel == null)
        {
            MessageBox.Show("אנא בחר דגם תחילה", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // Show dialog to select source model from consolidated models
            var sourceModels = FilteredConsolidatedModels
                .Where(m => m.ConsolidatedModelId != SelectedConsolidatedModel.ConsolidatedModelId) // Exclude current model
                .ToList();

            if (!sourceModels.Any())
            {
                MessageBox.Show("אין דגמים זמינים להעתקה", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Create a simple selection dialog
            var dialog = new Window
            {
                Title = "בחר דגם מקור להעתקת מיפויים",
                Width = 700,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                FlowDirection = FlowDirection.RightToLeft
            };

            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

            var listBox = new System.Windows.Controls.ListBox
            {
                ItemsSource = sourceModels,
                Margin = new Thickness(10)
            };

            // Custom display template for consolidated models
            listBox.ItemTemplate = CreateConsolidatedModelItemTemplate();
            System.Windows.Controls.Grid.SetRow(listBox, 0);

            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10)
            };

            var okButton = new System.Windows.Controls.Button
            {
                Content = "העתק",
                Width = 100,
                Height = 30,
                Margin = new Thickness(5)
            };
            okButton.Click += (s, e) => { dialog.DialogResult = true; dialog.Close(); };

            var cancelButton = new System.Windows.Controls.Button
            {
                Content = "ביטול",
                Width = 100,
                Height = 30,
                Margin = new Thickness(5)
            };
            cancelButton.Click += (s, e) => { dialog.Close(); };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            System.Windows.Controls.Grid.SetRow(buttonPanel, 1);

            grid.Children.Add(listBox);
            grid.Children.Add(buttonPanel);
            dialog.Content = grid;

            if (dialog.ShowDialog() == true && listBox.SelectedItem is ConsolidatedVehicleModel selectedSource)
            {
                IsLoading = true;
                StatusMessage = "מעתיק מיפויים...";

                // Get parts from source consolidated model
                var sourceParts = await _dataService.LoadMappedPartsForConsolidatedModelAsync(
                    selectedSource.ConsolidatedModelId,
                    includeCouplings: true);

                var sourcePartNumbers = sourceParts.Select(p => p.PartNumber).ToList();

                // Map to target consolidated model
                await _dataService.MapPartsToConsolidatedModelAsync(
                    SelectedConsolidatedModel.ConsolidatedModelId,
                    sourcePartNumbers,
                    "current_user");

                var sourceYearRange = selectedSource.YearTo.HasValue
                    ? $"{selectedSource.YearFrom}-{selectedSource.YearTo}"
                    : $"{selectedSource.YearFrom}+";
                var targetYearRange = SelectedConsolidatedModel.YearTo.HasValue
                    ? $"{SelectedConsolidatedModel.YearFrom}-{SelectedConsolidatedModel.YearTo}"
                    : $"{SelectedConsolidatedModel.YearFrom}+";

                StatusMessage = $"הועתקו {sourcePartNumbers.Count} חלקים";

                MessageBox.Show(
                    $"הועתקו בהצלחה {sourcePartNumbers.Count} חלקים מ-{selectedSource.ModelName} ({sourceYearRange}) ל-{SelectedConsolidatedModel.ModelName} ({targetYearRange})",
                    "הצלחה",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Reload parts
                await LoadMappedPartsForConsolidatedModelAsync(SelectedConsolidatedModel);
                await LoadSuggestedPartsForConsolidatedModelAsync(SelectedConsolidatedModel);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה: {ex.Message}";
            MessageBox.Show($"שגיאה בהעתקת מיפויים: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private DataTemplate CreateConsolidatedModelItemTemplate()
    {
        // Create a simple text template showing model info
        var template = new DataTemplate();
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding
        {
            Converter = new ConsolidatedModelDisplayConverter()
        });
        template.VisualTree = factory;
        return template;
    }

    [RelayCommand]
    private async Task AcceptPartSuggestionAsync(PartDisplayModel? part)
    {
        if (part == null || SelectedConsolidatedModel == null)
            return;

        try
        {
            IsLoading = true;
            StatusMessage = "ממפה חלק...";

            await _dataService.MapPartsToConsolidatedModelAsync(
                SelectedConsolidatedModel.ConsolidatedModelId,
                new List<string> { part.PartNumber },
                "current_user");

            var yearRange = SelectedConsolidatedModel.YearTo.HasValue
                ? $"{SelectedConsolidatedModel.YearFrom}-{SelectedConsolidatedModel.YearTo}"
                : $"{SelectedConsolidatedModel.YearFrom}+";
            StatusMessage = $"✓ מופה החלק {part.PartNumber} למודל {SelectedConsolidatedModel.ModelName} ({yearRange})";

            // Reload both lists
            await LoadMappedPartsForConsolidatedModelAsync(SelectedConsolidatedModel);
            await LoadSuggestedPartsForConsolidatedModelAsync(SelectedConsolidatedModel);
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה: {ex.Message}";
            MessageBox.Show($"שגיאה באישור הצעה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ShowCouplingInfoAsync(ConsolidatedVehicleModel? model)
    {
        if (model == null) return;

        try
        {
            // Get couplings for this model
            var couplings = await _dataService.GetModelCouplingsAsync(model.ConsolidatedModelId);
            var activeCouplings = couplings.Where(c => c.IsActive).ToList();

            if (!activeCouplings.Any())
            {
                MessageBox.Show(
                    $"הדגם '{model.Manufacturer?.ManufacturerShortName} {model.ModelName}' לא מצומד לדגמים אחרים.",
                    "מידע על צימוד",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // Get coupled model names
            var coupledModelNames = new List<string>();
            foreach (var coupling in activeCouplings)
            {
                var otherModelId = coupling.ConsolidatedModelIdA == model.ConsolidatedModelId
                    ? coupling.ConsolidatedModelIdB
                    : coupling.ConsolidatedModelIdA;

                var otherModel = await _dataService.GetConsolidatedModelByIdAsync(otherModelId);
                if (otherModel != null)
                {
                    var yearRange = otherModel.YearTo.HasValue
                        ? $"{otherModel.YearFrom}-{otherModel.YearTo}"
                        : $"{otherModel.YearFrom}+";
                    coupledModelNames.Add($"• {otherModel.Manufacturer?.ManufacturerShortName} {otherModel.ModelName} ({yearRange})");
                }
            }

            var yearRangeDisplay = model.YearTo.HasValue
                ? $"{model.YearFrom}-{model.YearTo}"
                : $"{model.YearFrom}+";

            var coupledModelsText = string.Join("\n", coupledModelNames);

            MessageBox.Show(
                $"🔗 הדגם '{model.Manufacturer?.ManufacturerShortName} {model.ModelName}' ({yearRangeDisplay})\n" +
                $"מצומד עם {activeCouplings.Count} דגמים:\n\n" +
                $"{coupledModelsText}\n\n" +
                "החלקים הממופים לדגמים אלו משותפים לכולם.\n" +
                "לניהול הצימודים, עבור לכרטיסייה 'ניהול צימודים'.",
                "מידע על צימוד",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"שגיאה בטעינת מידע צימוד: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Check if a model has active couplings (for UI binding)
    /// </summary>
    public async Task<bool> HasCouplingsAsync(int consolidatedModelId)
    {
        try
        {
            var couplings = await _dataService.GetModelCouplingsAsync(consolidatedModelId);
            return couplings.Any(c => c.IsActive);
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Converter to display consolidated model in a readable format
/// </summary>
public class ConsolidatedModelDisplayConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is ConsolidatedVehicleModel model)
        {
            var mfg = model.Manufacturer?.ManufacturerShortName ?? model.Manufacturer?.ManufacturerName ?? "לא ידוע";
            var yearRange = model.YearTo.HasValue
                ? $"{model.YearFrom}-{model.YearTo}"
                : $"{model.YearFrom}+";
            var engineVol = model.EngineVolume.HasValue ? $"{model.EngineVolume} סמ\"ק" : "";
            var fuel = !string.IsNullOrEmpty(model.FuelTypeName) ? model.FuelTypeName : "";

            var parts = new List<string> { mfg, "-", model.ModelName ?? "" };
            if (!string.IsNullOrEmpty(engineVol)) parts.Add(engineVol);
            if (!string.IsNullOrEmpty(fuel)) parts.Add(fuel);
            parts.Add($"({yearRange})");

            return string.Join(" ", parts);
        }
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
