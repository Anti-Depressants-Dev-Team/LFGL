using LFGL.Features.Settings;
using System.IO;

namespace LFGL.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly AppSettings _settings;

    public SettingsPage()
    {
        this.InitializeComponent();
        _settings = AppSettings.Instance;
        
        // Load current values
        StartOnStartupToggle.IsOn = _settings.StartOnStartup;
        AutoScanToggle.IsOn = _settings.AutoScanOnStartup;
        CloseToTrayToggle.IsOn = _settings.CloseToTray;
    }

    private void StartOnStartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        _settings.StartOnStartup = StartOnStartupToggle.IsOn;
        _settings.Save();
    }

    private void AutoScanToggle_Toggled(object sender, RoutedEventArgs e)
    {
        _settings.AutoScanOnStartup = AutoScanToggle.IsOn;
        _settings.Save();
    }

    private void CloseToTrayToggle_Toggled(object sender, RoutedEventArgs e)
    {
        _settings.CloseToTray = CloseToTrayToggle.IsOn;
        _settings.Save();
    }

    private async void ClearCacheBtn_Click(object sender, RoutedEventArgs e)
    {
        var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LFGL");
        
        try
        {
            var iconCache = Path.Combine(cacheDir, "IconCache");
            var steamArt = Path.Combine(cacheDir, "SteamArt");
            
            if (Directory.Exists(iconCache)) Directory.Delete(iconCache, true);
            if (Directory.Exists(steamArt)) Directory.Delete(steamArt, true);

            var dialog = new ContentDialog
            {
                Title = "Cache Cleared",
                Content = "Icon cache has been cleared. Re-scan to download fresh icons.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Error",
                Content = $"Failed to clear cache: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
