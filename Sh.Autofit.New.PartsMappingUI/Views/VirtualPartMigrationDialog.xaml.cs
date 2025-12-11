using Sh.Autofit.New.PartsMappingUI.Models;
using Sh.Autofit.New.PartsMappingUI.Services;
using System.Windows;

namespace Sh.Autofit.New.PartsMappingUI.Views;

public partial class VirtualPartMigrationDialog : Window
{
    private readonly IVirtualPartAutoMappingService _autoMappingService;
    private readonly VirtualPartMigrationCandidate _candidate;

    public bool MigrationCompleted { get; private set; }

    public VirtualPartMigrationDialog(
        IVirtualPartAutoMappingService autoMappingService,
        VirtualPartMigrationCandidate candidate)
    {
        InitializeComponent();

        _autoMappingService = autoMappingService;
        _candidate = candidate;

        // Populate UI
        VirtualPartNumberText.Text = candidate.VirtualPartNumber;
        VirtualPartNameText.Text = candidate.VirtualPartName;
        RealPartNumberText.Text = candidate.RealPartNumber;
        RealPartNameText.Text = candidate.RealPartName;
        MatchedOemsText.Text = string.Join(" | ", candidate.MatchedOemNumbers);
        MappingsCountText.Text = candidate.MappingsToTransfer.ToString();
    }

    private async void MigrateButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            $"האם אתה בטוח שברצונך להעביר {_candidate.MappingsToTransfer} מיפויים\n" +
            $"מהחלק הוירטואלי '{_candidate.VirtualPartNumber}'\n" +
            $"לחלק האמיתי '{_candidate.RealPartNumber}'?\n\n" +
            $"החלק הוירטואלי יימחק.",
            "אישור העברה",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            // Disable button to prevent double-click
            var button = sender as System.Windows.Controls.Button;
            if (button != null)
                button.IsEnabled = false;

            var migrationResult = await _autoMappingService.MigrateVirtualPartAsync(
                _candidate.VirtualPartId,
                _candidate.RealPartNumber,
                "User");

            if (migrationResult.Success)
            {
                MessageBox.Show(
                    $"✓ ההעברה הושלמה בהצלחה!\n\n" +
                    $"מיפויים שהועברו: {migrationResult.MappingsTransferred}\n" +
                    $"החלק הוירטואלי נמחק\n" +
                    $"הפעולה נרשמה ביומן",
                    "הצלחה",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                MigrationCompleted = true;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show(
                    $"❌ ההעברה נכשלה:\n\n{migrationResult.ErrorMessage}",
                    "שגיאה",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                if (button != null)
                    button.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"שגיאה בהעברת החלק:\n\n{ex.Message}",
                "שגיאה",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
