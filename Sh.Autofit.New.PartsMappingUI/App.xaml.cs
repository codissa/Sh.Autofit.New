using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Services;
using Sh.Autofit.New.PartsMappingUI.ViewModels;
using System.Windows;

namespace Sh.Autofit.New.PartsMappingUI;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Connection string - TODO: Move to configuration file
        const string connectionString =
            "Data Source=server-pc\\wizsoft2;Initial Catalog=Sh.Autofit;Persist Security Info=True;User ID=issa;Password=5060977Ih;Encrypt=False;Trust Server Certificate=True";

        // Register DbContext factory
        services.AddDbContextFactory<ShAutofitContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
                sqlOptions.CommandTimeout(60);
            });
            options.EnableSensitiveDataLogging(false);
        });

        // Register services
        services.AddSingleton<IDataService, DataService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IPartSuggestionService, PartSuggestionService>();
        services.AddSingleton<IGovernmentApiService, GovernmentApiService>();
        services.AddSingleton<IVehicleMatchingService, VehicleMatchingService>();

        // Register ViewModels
        services.AddSingleton<MappingViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<PlateLookupViewModel>();

        // Register Views
        services.AddSingleton<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}

