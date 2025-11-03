using CommunityToolkit.Mvvm.ComponentModel;

namespace Sh.Autofit.New.PartsMappingUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private int _selectedTabIndex;

    public MappingViewModel MappingViewModel { get; }
    public PlateLookupViewModel PlateLookupViewModel { get; }

    public MainViewModel(MappingViewModel mappingViewModel, PlateLookupViewModel plateLookupViewModel)
    {
        MappingViewModel = mappingViewModel;
        PlateLookupViewModel = plateLookupViewModel;
    }
}
