using System.Windows;

namespace Sh.Autofit.New.PartsMappingUI.Views;

public partial class CreateKitDialog : Window
{
    public string KitName { get; private set; } = string.Empty;
    public string? KitDescription { get; private set; }

    public CreateKitDialog(string? existingName = null, string? existingDescription = null)
    {
        InitializeComponent();

        if (!string.IsNullOrEmpty(existingName))
        {
            KitNameTextBox.Text = existingName;
            Title = "ערוך ערכת חלקים";
        }

        if (!string.IsNullOrEmpty(existingDescription))
        {
            DescriptionTextBox.Text = existingDescription;
        }

        KitNameTextBox.Focus();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var kitName = KitNameTextBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(kitName))
        {
            MessageBox.Show("אנא הזן שם לערכה", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
            KitNameTextBox.Focus();
            return;
        }

        KitName = kitName;
        KitDescription = string.IsNullOrWhiteSpace(DescriptionTextBox.Text)
            ? null
            : DescriptionTextBox.Text.Trim();

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
