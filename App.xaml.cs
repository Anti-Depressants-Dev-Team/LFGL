using LFGL.Features.Library;
using LFGL.Features.Scanning;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;

namespace LFGL;

public partial class App : Application
{
    public IServiceProvider Services { get; }
    public static MainWindow? MainWindow { get; private set; }
    
    private static Mutex? _mutex;
    private const string MutexName = "LFGL_SingleInstance_Mutex";

    public App()
    {
        this.InitializeComponent();

        // Single instance check
        _mutex = new Mutex(true, MutexName, out bool isNewInstance);
        if (!isNewInstance)
        {
            // Another instance is already running - exit
            Environment.Exit(0);
            return;
        }

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<GameLibrary>();
        services.AddTransient<IGameScannerService, GameScannerService>();
        services.AddTransient<MainWindow>();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = Services.GetRequiredService<MainWindow>();
        MainWindow.Activate();
    }
}
