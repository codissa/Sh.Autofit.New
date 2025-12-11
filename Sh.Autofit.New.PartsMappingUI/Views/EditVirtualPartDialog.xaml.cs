using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Services;
using System.Windows;

namespace Sh.Autofit.New.PartsMappingUI.Views;

public partial class EditVirtualPartDialog : Window
{
    private readonly IVirtualPartService _virtualPartService;
    private readonly VirtualPart _virtualPart;

    public bool WasUpdated { get; private set; }

    public EditVirtualPartDialog(
        IVirtualPartService virtualPartService,
        VirtualPart virtualPart,
        List<string>? categories = null)
    {
        InitializeComponent();

        _virtualPartService = virtualPartService;
        _virtualPart = virtualPart;

        // Populate fields with existing values
        PartNumberTextBox.Text = virtualPart.PartNumber;
        Oem1TextBox.Text = virtualPart.OemNumber1;
        Oem2TextBox.Text = virtualPart.OemNumber2 ?? "";
        Oem3TextBox.Text = virtualPart.OemNumber3 ?? "";
        Oem4TextBox.Text = virtualPart.OemNumber4 ?? "";
        Oem5TextBox.Text = virtualPart.OemNumber5 ?? "";
        DescriptionTextBox.Text = virtualPart.PartName;
        NotesTextBox.Text = virtualPart.Notes;

        // Populate categories
        if (categories != null && categories.Any())
        {
            foreach (var category in categories)
            {
                CategoryComboBox.Items.Add(category);
            }

            if (!string.IsNullOrEmpty(virtualPart.Category))
            {
                CategoryComboBox.SelectedItem = virtualPart.Category;
            }
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
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

        if (string.IsNullOrWhiteSpace(NotesTextBox.Text))
        {
            MessageBox.Show("הערות הוא שדה חובה", "שגיאה",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            NotesTextBox.Focus();
            return;
        }

        try
        {
            await _virtualPartService.UpdateVirtualPartAsync(
                virtualPartId: _virtualPart.VirtualPartId,
                description: DescriptionTextBox.Text.Trim(),
                notes: NotesTextBox.Text.Trim(),
                oem1: Oem1TextBox.Text.Trim(),
                oem2: string.IsNullOrWhiteSpace(Oem2TextBox.Text) ? null : Oem2TextBox.Text.Trim(),
                oem3: string.IsNullOrWhiteSpace(Oem3TextBox.Text) ? null : Oem3TextBox.Text.Trim(),
                oem4: string.IsNullOrWhiteSpace(Oem4TextBox.Text) ? null : Oem4TextBox.Text.Trim(),
                oem5: string.IsNullOrWhiteSpace(Oem5TextBox.Text) ? null : Oem5TextBox.Text.Trim(),
                category: CategoryComboBox.Text?.Trim(),
                updatedBy: "User"
            );

            WasUpdated = true;

            MessageBox.Show(
                $"החלק הוירטואלי עודכן בהצלחה!\n\n" +
                $"מספר חלק: {_virtualPart.PartNumber}\n" +
                $"תיאור: {DescriptionTextBox.Text.Trim()}",
                "✓ הצלחה",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"שגיאה בעדכון חלק וירטואלי:\n\n{ex.Message}",
                "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
