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

    private StockMoveViewModel? GetStockMoveVM()
    {
        return (DataContext as MainViewModel)?.StockMoveVM;
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

    private void StockIdTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var vm = GetStockMoveVM();
            if (vm?.LoadStockCommand.CanExecute(null) == true)
            {
                vm.LoadStockCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void QuantityTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var vm = GetPrintOnDemandVM();
            if (vm?.PrintCommand.CanExecute(null) == true)
            {
                vm.PrintCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void StockItemsListView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var vm = GetStockMoveVM();
        if (vm == null || vm.Items.Count == 0) return;

        var listView = sender as ListView;
        if (listView == null) return;

        switch (e.Key)
        {
            case Key.Up:
                if (listView.SelectedIndex > 0)
                {
                    listView.SelectedIndex--;
                    listView.ScrollIntoView(listView.SelectedItem);
                }
                e.Handled = true;
                break;

            case Key.Down:
                if (listView.SelectedIndex < vm.Items.Count - 1)
                {
                    listView.SelectedIndex++;
                    listView.ScrollIntoView(listView.SelectedItem);
                }
                e.Handled = true;
                break;

            case Key.Delete:
                if (listView.SelectedItem is StockMoveLabelItem itemToDelete)
                {
                    vm.RemoveItemCommand.Execute(itemToDelete);
                }
                e.Handled = true;
                break;

            case Key.Enter:
                // If Arabic mode and item selected, open Arabic edit dialog
                if (listView.SelectedItem is StockMoveLabelItem item && item.IsArabic)
                {
                    vm.EditArabicCommand.Execute(item);
                    e.Handled = true;
                }
                break;

            case Key.Home:
                if (vm.Items.Count > 0)
                {
                    listView.SelectedIndex = 0;
                    listView.ScrollIntoView(listView.SelectedItem);
                }
                e.Handled = true;
                break;

            case Key.End:
                if (vm.Items.Count > 0)
                {
                    listView.SelectedIndex = vm.Items.Count - 1;
                    listView.ScrollIntoView(listView.SelectedItem);
                }
                e.Handled = true;
                break;
        }
    }
}
