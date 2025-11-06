using System.Windows;
using System.Windows.Controls;
using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Views;

public partial class SmartSuggestionsView : UserControl
{
    public SmartSuggestionsView()
    {
        InitializeComponent();
    }

    private void SelectAllTargets_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is SmartSuggestion suggestion)
        {
            foreach (var target in suggestion.TargetModels)
            {
                target.IsSelected = true;
            }
        }
    }

    private void DeselectAllTargets_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is SmartSuggestion suggestion)
        {
            foreach (var target in suggestion.TargetModels)
            {
                target.IsSelected = false;
            }
        }
    }
}
