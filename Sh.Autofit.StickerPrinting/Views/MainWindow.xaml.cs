using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Sh.Autofit.StickerPrinting.Models;
using Sh.Autofit.StickerPrinting.ViewModels;

namespace Sh.Autofit.StickerPrinting.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private PrintOnDemandViewModel? GetPrintOnDemandVM()
    {
        return (DataContext as MainViewModel)?.PrintOnDemandVM;
    }

    private void ItemKeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var vm = GetPrintOnDemandVM();
        if (vm == null) return;

        if (e.Key == Key.Down && vm.SearchResults.Count > 0)
        {
            // Focus the suggestions list
            SuggestionsList.Focus();
            if (SuggestionsList.Items.Count > 0)
                SuggestionsList.SelectedIndex = 0;
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.IsDropdownOpen = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && !vm.IsDropdownOpen)
        {
            vm.LoadItemCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && vm.IsDropdownOpen && vm.SearchResults.Count > 0)
        {
            // Select first item if dropdown is open
            vm.SelectSuggestionCommand.Execute(vm.SearchResults[0]);
            ItemKeyTextBox.Focus();
            e.Handled = true;
        }
    }

    private void SuggestionsList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var vm = GetPrintOnDemandVM();
        if (vm == null) return;

        if (e.Key == Key.Enter && SuggestionsList.SelectedItem is PartInfo selected)
        {
            vm.SelectSuggestionCommand.Execute(selected);
            ItemKeyTextBox.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.IsDropdownOpen = false;
            ItemKeyTextBox.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Up && SuggestionsList.SelectedIndex == 0)
        {
            // Return focus to textbox when pressing up at the top
            ItemKeyTextBox.Focus();
            e.Handled = true;
        }
    }

    private void SuggestionsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var vm = GetPrintOnDemandVM();
        if (vm == null) return;

        if (SuggestionsList.SelectedItem is PartInfo selected)
        {
            vm.SelectSuggestionCommand.Execute(selected);
            ItemKeyTextBox.Focus();
        }
    }
}
