using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace LFGL.Features.Settings;

public class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
        "LFGL", "settings.json");

    public bool StartOnStartup { get; set; } = false;
    public bool CloseToTray { get; set; } = false;
    public bool AutoScanOnStartup { get; set; } = false;

    private static AppSettings? _instance;
    public static AppSettings Instance => _instance ??= Load();

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
            
            // Apply startup setting
            ApplyStartupSetting();
        }
        catch { }
    }

    private void ApplyStartupSetting()
    {
        const string appName = "LFGL";
        var exePath = Environment.ProcessPath ?? "";
        
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
        if (key == null) return;

        if (StartOnStartup)
        {
            key.SetValue(appName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(appName, false);
        }
    }
}
