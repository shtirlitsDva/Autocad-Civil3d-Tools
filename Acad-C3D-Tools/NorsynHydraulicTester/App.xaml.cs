using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using NorsynHydraulicTester.Services;
using NorsynHydraulicTester.ViewModels;

namespace NorsynHydraulicTester;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        var mainWindow = new MainWindow
        {
            DataContext = Services.GetRequiredService<MainViewModel>()
        };
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ICalculationService, CalculationService>();

        services.AddSingleton<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SegmentInputViewModel>();
        services.AddTransient<CalculationViewModel>();
        services.AddTransient<LookupTableViewModel>();
    }
}
