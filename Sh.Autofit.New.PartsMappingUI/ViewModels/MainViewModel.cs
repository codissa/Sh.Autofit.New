using CommunityToolkit.Mvvm.ComponentModel;

namespace Sh.Autofit.New.PartsMappingUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private int _selectedTabIndex;

    public MappingViewModel MappingViewModel { get; }
    public PlateLookupViewModel PlateLookupViewModel { get; }
    public PartKitsViewModel PartKitsViewModel { get; }

    public MainViewModel(MappingViewModel mappingViewModel, PlateLookupViewModel plateLookupViewModel, PartKitsViewModel partKitsViewModel)
    {
        MappingViewModel = mappingViewModel;
        PlateLookupViewModel = plateLookupViewModel;
        PartKitsViewModel = partKitsViewModel;
    }
}
