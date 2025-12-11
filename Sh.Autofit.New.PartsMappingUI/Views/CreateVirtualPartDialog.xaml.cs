using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Services;
using System.Windows;

namespace Sh.Autofit.New.PartsMappingUI.Views;

public partial class CreateVirtualPartDialog : Window
{
    private readonly IVirtualPartService _virtualPartService;
    private readonly int? _consolidatedModelId;
    private readonly int? _vehicleTypeId;

    public VirtualPart? CreatedVirtualPart { get; private set; }

    public CreateVirtualPartDialog(
        IVirtualPartService virtualPartService,
        int? consolidatedModelId = null,
        int? vehicleTypeId = null,
        List<string>? categories = null)
    {
        InitializeComponent();

        _virtualPartService = virtualPartService;
        _consolidatedModelId = consolidatedModelId;
        _vehicleTypeId = vehicleTypeId;

        // Populate categories
        if (categories != null && categories.Any())
        {
            foreach (var category in categories)
            {
                CategoryComboBox.Items.Add(category);
            }
        }
    }

    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(Oem1TextBox.Text))
        {
            MessageBox.Show("מספר OEM 1 הוא שדה חובה", "שגיאה",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            Oem1TextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
        {
            MessageBox.Show("תיאור הוא שדה חובה", "שגיאה",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            DescriptionTextBox.Focus();
            return;
        }

        try
        {
            CreatedVirtualPart = await _virtualPartService.CreateVirtualPartAsync(
                description: DescriptionTextBox.Text.Trim(),
                notes: string.IsNullOrWhiteSpace(NotesTextBox.Text) ? "" : NotesTextBox.Text.Trim(),
                oem1: Oem1TextBox.Text.Trim(),
                oem2: string.IsNullOrWhiteSpace(Oem2TextBox.Text) ? null : Oem2TextBox.Text.Trim(),
                oem3: string.IsNullOrWhiteSpace(Oem3TextBox.Text) ? null : Oem3TextBox.Text.Trim(),
                oem4: string.IsNullOrWhiteSpace(Oem4TextBox.Text) ? null : Oem4TextBox.Text.Trim(),
                oem5: string.IsNullOrWhiteSpace(Oem5TextBox.Text) ? null : Oem5TextBox.Text.Trim(),
                category: CategoryComboBox.Text?.Trim(),
                vehicleTypeId: _vehicleTypeId,
                consolidatedModelId: _consolidatedModelId,
                createdBy: "User"
            );

            MessageBox.Show(
                $"חלק וירטואלי נוצר בהצלחה!\n\n" +
                $"מספר חלק: {CreatedVirtualPart.PartNumber}\n" +
                $"תיאור: {CreatedVirtualPart.PartName}\n\n" +
                $"החלק מוכן למיפוי לרכב.",
                "✓ הצלחה",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"שגיאה ביצירת חלק וירטואלי:\n\n{ex.Message}",
                "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
