using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Models;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Sh.Autofit.New.PartsMappingUI.Views;

public partial class SelectConsolidatedModelsDialog : Window, INotifyPropertyChanged
{
    private List<SelectableConsolidatedModel> _allModels;
    private List<SelectableConsolidatedModel> _filteredModels;

    public List<ConsolidatedVehicleModel> SelectedModels { get; private set; }

    public SelectConsolidatedModelsDialog(List<ConsolidatedVehicleModel> models)
    {
        InitializeComponent();
        DataContext = this;

        // Wrap models in selectable wrapper
        _allModels = models.Select(m => new SelectableConsolidatedModel(m)).ToList();
        _filteredModels = new List<SelectableConsolidatedModel>(_allModels);

        // Subscribe to selection changes
        foreach (var model in _allModels)
        {
            model.PropertyChanged += Model_PropertyChanged;
        }

        ModelsGrid.ItemsSource = _filteredModels;
        SelectedModels = new List<ConsolidatedVehicleModel>();

        UpdateSelectionCount();
    }

    private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectableConsolidatedModel.IsSelected))
        {
            UpdateSelectionCount();
        }
    }

    private void UpdateSelectionCount()
    {
        var selectedCount = _allModels.Count(m => m.IsSelected);
        SelectionCountText.Text = selectedCount == 0
            ? "לא נבחרו דגמים"
            : $"נבחרו {selectedCount} דגמים";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchBox.Text?.ToLower() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            _filteredModels = new List<SelectableConsolidatedModel>(_allModels);
        }
        else
        {
            _filteredModels = _allModels.Where(m =>
                m.Model.Manufacturer?.ManufacturerName?.ToLower().Contains(searchText) == true ||
                m.Model.Manufacturer?.ManufacturerShortName?.ToLower().Contains(searchText) == true ||
                m.Model.ModelName?.ToLower().Contains(searchText) == true ||
                m.Model.FuelTypeName?.ToLower().Contains(searchText) == true ||
                m.Model.TransmissionType?.ToLower().Contains(searchText) == true ||
                m.Model.TrimLevel?.ToLower().Contains(searchText) == true ||
                m.Model.YearFrom.ToString().Contains(searchText) ||
                (m.Model.YearTo?.ToString().Contains(searchText) == true)
            ).ToList();
        }

        ModelsGrid.ItemsSource = _filteredModels;
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedModels = _allModels.Where(m => m.IsSelected).Select(m => m.Model).ToList();

        if (!SelectedModels.Any())
        {
            MessageBox.Show("אנא בחר לפחות דגם אחד", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
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

    public event PropertyChangedEventHandler? PropertyChanged;
}

// Wrapper class to make ConsolidatedVehicleModel selectable
public class SelectableConsolidatedModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public ConsolidatedVehicleModel Model { get; }

    // Expose all properties from the wrapped model for binding
    public int ConsolidatedModelId => Model.ConsolidatedModelId;
    public Sh.Autofit.New.Entities.Models.Manufacturer? Manufacturer => Model.Manufacturer;
    public string? ModelName => Model.ModelName;
    public int YearFrom => Model.YearFrom;
    public int? YearTo => Model.YearTo;
    public int? EngineVolume => Model.EngineVolume;
    public string? TransmissionType => Model.TransmissionType;
    public string? FuelTypeName => Model.FuelTypeName;
    public string? TrimLevel => Model.TrimLevel;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public SelectableConsolidatedModel(ConsolidatedVehicleModel model)
    {
        Model = model;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
