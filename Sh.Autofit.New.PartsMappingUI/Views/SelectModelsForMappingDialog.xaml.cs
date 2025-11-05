using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Helpers;
using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Views
{
    public partial class SelectModelsForMappingDialog : Window
    {
        private readonly IDbContextFactory<ShAutofitContext> _contextFactory;
        private readonly ObservableCollection<PartDisplayModel> _allParts;
        private readonly ObservableCollection<PartDisplayModel> _filteredParts;
        private readonly ObservableCollection<VehicleModelDisplayModel> _allModels;
        private readonly ObservableCollection<VehicleModelDisplayModel> _filteredModels;

        public List<PartDisplayModel> SelectedParts { get; private set; } = new List<PartDisplayModel>();
        public List<VehicleModelDisplayModel> SelectedModels { get; private set; } = new List<VehicleModelDisplayModel>();

        public SelectModelsForMappingDialog(
            IDbContextFactory<ShAutofitContext> contextFactory,
            VehicleDisplayModel currentVehicle,
            List<PartDisplayModel>? initialParts = null)
        {
            InitializeComponent();

            _contextFactory = contextFactory;
            _allParts = new ObservableCollection<PartDisplayModel>(initialParts ?? new List<PartDisplayModel>());
            _filteredParts = new ObservableCollection<PartDisplayModel>(_allParts);
            _allModels = new ObservableCollection<VehicleModelDisplayModel>();
            _filteredModels = new ObservableCollection<VehicleModelDisplayModel>();

            PartsGrid.ItemsSource = _filteredParts;
            ModelsGrid.ItemsSource = _filteredModels;

            // Mark all initial parts as selected
            foreach (var part in _allParts)
            {
                part.IsSelected = true;
            }

            // Load similar models and unmapped parts if needed
            Loaded += async (s, e) =>
            {
                if (initialParts == null || !initialParts.Any())
                {
                    // Load unmapped parts for quick map scenario
                    await LoadUnmappedPartsAsync(currentVehicle.VehicleTypeId);
                }
                await LoadSimilarModelsAsync(currentVehicle);
            };
        }

        private async System.Threading.Tasks.Task LoadUnmappedPartsAsync(int vehicleTypeId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Get already mapped parts for this vehicle
            var mappedPartNumbers = await context.VehiclePartsMappings
                .Where(vpm => vpm.VehicleTypeId == vehicleTypeId)
                .Select(vpm => vpm.PartItemKey)
                .Distinct()
                .ToListAsync();

            // Get all active parts that are not mapped
            var unmappedParts = await context.VwParts
                .AsNoTracking()
                .Where(p => p.IsActive == 1 && !mappedPartNumbers.Contains(p.PartNumber))
                .OrderBy(p => p.PartNumber)
                .Select(p => new PartDisplayModel
                {
                    PartNumber = p.PartNumber,
                    PartName = p.PartName,
                    Category = p.Category ?? string.Empty,
                    Manufacturer = p.Manufacturer ?? string.Empty,
                    IsSelected = false
                })
                .ToListAsync();

            foreach (var part in unmappedParts)
            {
                _allParts.Add(part);
                _filteredParts.Add(part);
            }
        }

        private async System.Threading.Tasks.Task LoadSimilarModelsAsync(VehicleDisplayModel currentVehicle)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Get current vehicle details
            var currentVehicleType = await context.VehicleTypes
                .AsNoTracking()
                .Include(v => v.Manufacturer)
                .FirstOrDefaultAsync(v => v.VehicleTypeId == currentVehicle.VehicleTypeId);

            if (currentVehicleType == null || !currentVehicleType.EngineVolume.HasValue)
                return;

            var engineVolume = currentVehicleType.EngineVolume.Value;
            var yearFrom = currentVehicleType.YearFrom;
            var commercialName = currentVehicleType.CommercialName ?? string.Empty;
            var currentModelNameNormalized = currentVehicleType.ModelName.NormalizeForGrouping();

            // Find similar vehicles:
            // - Must have overlapping engine volume
            // - Must have overlapping years
            // - Ignore model name (don't compare)
            // - Give bonus score for same commercial name

            var similarVehicles = await context.VehicleTypes
                .AsNoTracking()
                .Include(v => v.Manufacturer)
                .Where(v => v.IsActive &&
                           v.VehicleTypeId != currentVehicle.VehicleTypeId &&
                           v.EngineVolume.HasValue &&
                           v.EngineVolume.Value == engineVolume)
                .ToListAsync();

            // Group by model name to avoid duplicates
            var groupedVehicles = similarVehicles
                .Where(v => v.YearFrom == yearFrom) // Year overlap check
                .GroupBy(v => new
                {
                    ManufacturerName = v.Manufacturer?.ManufacturerName ?? string.Empty,
                    ManufacturerShortName = v.Manufacturer?.ManufacturerShortName ?? string.Empty,
                    v.ModelName,
                    v.CommercialName
                })
                .Select(g =>
                {
                    var first = g.First();
                    var modelNameNormalized = first.ModelName.NormalizeForGrouping();

                    // Skip if same model name (ignore whitespace)
                    if (modelNameNormalized == currentModelNameNormalized)
                        return null;

                    // Calculate score
                    int score = 50; // Base score for matching engine + year

                    // Bonus for same commercial name (bonus, not required)
                    if (!string.IsNullOrEmpty(first.CommercialName) &&
                        !string.IsNullOrEmpty(commercialName) &&
                        first.CommercialName.EqualsIgnoringWhitespace(commercialName))
                    {
                        score += 50;
                    }

                    return new VehicleModelDisplayModel
                    {
                        VehicleTypeId = first.VehicleTypeId,
                        ManufacturerName = first.Manufacturer?.ManufacturerName ?? string.Empty,
                        ManufacturerShortName = first.Manufacturer?.ManufacturerShortName ?? first.Manufacturer?.ManufacturerName ?? string.Empty,
                        ModelName = first.ModelName,
                        CommercialName = first.CommercialName ?? string.Empty,
                        YearFrom = g.Min(v => v.YearFrom),
                        YearTo = g.Max(v => v.YearFrom),
                        EngineVolume = first.EngineVolume,
                        Score = score,
                        IsSelected = true // Select all by default
                    };
                })
                .Where(m => m != null)
                .OrderByDescending(m => m!.Score)
                .ThenBy(m => m!.DisplayName)
                .Cast<VehicleModelDisplayModel>()
                .ToList();

            foreach (var model in groupedVehicles)
            {
                _allModels.Add(model);
                _filteredModels.Add(model);
            }

            UpdateModelsCount();
        }

        private void PartsSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = PartsSearchBox.Text.ToLower();

            _filteredParts.Clear();
            var filtered = string.IsNullOrWhiteSpace(searchText)
                ? _allParts
                : _allParts.Where(p =>
                    (p.PartNumber?.ToLower().Contains(searchText) ?? false) ||
                    (p.PartName?.ToLower().Contains(searchText) ?? false));

            foreach (var part in filtered)
            {
                _filteredParts.Add(part);
            }
        }

        private void ModelsSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = ModelsSearchBox.Text.ToLower();

            _filteredModels.Clear();
            var filtered = string.IsNullOrWhiteSpace(searchText)
                ? _allModels
                : _allModels.Where(m =>
                    m.ModelName.ToLower().Contains(searchText) ||
                    m.ManufacturerName.ToLower().Contains(searchText) ||
                    (m.CommercialName?.ToLower().Contains(searchText) ?? false));

            foreach (var model in filtered)
            {
                _filteredModels.Add(model);
            }

            UpdateModelsCount();
        }

        private void UpdateModelsCount()
        {
            var selectedCount = _allModels.Count(m => m.IsSelected);
            ModelsCountText.Text = $"נמצאו {_allModels.Count} דגמים דומים | {selectedCount} מסומנים";
        }

        private async void AddPartsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Get all available parts
                var allParts = await context.VwParts
                    .AsNoTracking()
                    .Where(p => p.IsActive == 1)
                    .OrderBy(p => p.PartNumber)
                    .Select(p => new PartDisplayModel
                    {
                        PartNumber = p.PartNumber,
                        PartName = p.PartName,
                        Category = p.Category ?? string.Empty,
                        Manufacturer = p.Manufacturer ?? string.Empty,
                        IsSelected = false
                    })
                    .ToListAsync();

                var dialog = new SelectPartsDialog(allParts)
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    // Add selected parts that aren't already in the list
                    foreach (var newPart in dialog.SelectedParts)
                    {
                        if (!_allParts.Any(p => p.PartNumber == newPart.PartNumber))
                        {
                            newPart.IsSelected = true;
                            _allParts.Add(newPart);
                            _filteredParts.Add(newPart);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בטעינת חלקים: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MapButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedParts = _allParts.Where(p => p.IsSelected).ToList();
            SelectedModels = _allModels.Where(m => m.IsSelected).ToList();

            if (SelectedParts.Count == 0)
            {
                MessageBox.Show("אנא בחר לפחות חלק אחד למיפוי", "שים לב", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedModels.Count == 0)
            {
                MessageBox.Show("אנא בחר לפחות דגם אחד למיפוי", "שים לב", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
