using System.Windows;

namespace Sh.Autofit.StickerPrinting.Views;

public partial class EditArabicDialog : Window
{
    public string ArabicDescription
    {
        get => ArabicTextBox.Text;
        set => ArabicTextBox.Text = value;
    }

    public EditArabicDialog()
    {
        InitializeComponent();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
