using System.Windows;

namespace Sh.Autofit.StickerPrinting.Views;

public partial class AddItemDialog : Window
{
    public string ItemKey => ItemKeyTextBox.Text.Trim().ToUpperInvariant();

    public int Quantity
    {
        get => int.TryParse(QuantityTextBox.Text, out var qty) ? Math.Max(1, qty) : 1;
    }

    public AddItemDialog()
    {
        InitializeComponent();
        ItemKeyTextBox.Focus();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ItemKey))
        {
            MessageBox.Show("נא להזין מק''ט", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
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
