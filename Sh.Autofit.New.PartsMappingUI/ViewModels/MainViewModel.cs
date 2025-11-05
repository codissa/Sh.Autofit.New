using CommunityToolkit.Mvvm.ComponentModel;

namespace Sh.Autofit.New.PartsMappingUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private int _selectedTabIndex;

    public MappingViewModel MappingViewModel { get; }
    public PlateLookupViewModel PlateLookupViewModel { get; }
    public PartKitsViewModel PartKitsViewModel { get; }
    public PartMappingsManagementViewModel PartMappingsManagementViewModel { get; }
    public ModelMappingsManagementViewModel ModelMappingsManagementViewModel { get; }
    public AnalyticsDashboardViewModel AnalyticsViewModel { get; }

    public MainViewModel(
        MappingViewModel mappingViewModel,
        PlateLookupViewModel plateLookupViewModel,
        PartKitsViewModel partKitsViewModel,
        PartMappingsManagementViewModel partMappingsManagementViewModel,
        ModelMappingsManagementViewModel modelMappingsManagementViewModel,
        AnalyticsDashboardViewModel analyticsViewModel)
    {
        MappingViewModel = mappingViewModel;
        PlateLookupViewModel = plateLookupViewModel;
        PartKitsViewModel = partKitsViewModel;
        PartMappingsManagementViewModel = partMappingsManagementViewModel;
        ModelMappingsManagementViewModel = modelMappingsManagementViewModel;
        AnalyticsViewModel = analyticsViewModel;
    }
}
