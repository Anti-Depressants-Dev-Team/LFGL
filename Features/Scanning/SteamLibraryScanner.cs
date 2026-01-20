using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace LFGL.Features.Scanning;

public static partial class SteamLibraryScanner
{
    // Regex to parse parsing ACF/VDF files loosely
    // "appid" "12345"
    [GeneratedRegex("\"(?<key>\\w+)\"\\s+\"(?<value>.+)\"")]
    private static partial Regex KeyValueRegex();

    public static string? GetSteamUsername()
    {
        try
        {
            // Method 1: Try loginusers.vdf (has the actual username)
            var steamPath = GetSteamPath();
            if (!string.IsNullOrEmpty(steamPath))
            {
                var loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");
                if (File.Exists(loginUsersPath))
                {
                    var lines = File.ReadAllLines(loginUsersPath);
                    string? lastPersonaName = null;
                    
                    foreach (var line in lines)
                    {
                        var match = KeyValueRegex().Match(line);
                        if (!match.Success) continue;
                        
                        var propKey = match.Groups["key"].Value;
                        var val = match.Groups["value"].Value;
                        
                        if (propKey.Equals("PersonaName", StringComparison.OrdinalIgnoreCase))
                        {
                            lastPersonaName = val;
                        }
                        if (propKey.Equals("MostRecent", StringComparison.OrdinalIgnoreCase) && val == "1")
                        {
                            if (!string.IsNullOrEmpty(lastPersonaName))
                                return lastPersonaName;
                        }
                    }
                    
                    // Return last found if MostRecent wasn't tagged
                    if (!string.IsNullOrEmpty(lastPersonaName))
                        return lastPersonaName;
                }
            }

            // Method 2: Fallback to registry
            using var regKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
            if (regKey?.GetValue("LastGameNameUsed") is string name && !string.IsNullOrEmpty(name))
                return name;
        }
        catch { }
        
        return null;
    }

    public static List<(string Name, string AppId, string InstallDir, string LibraryPath)> ScanSteamLibraries()
    {
        var games = new List<(string, string, string, string)>();
        
        // 1. Find Steam Base installation
        string steamPath = GetSteamPath();
        if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath)) return games;

        var libraryFolders = new List<string> { Path.Combine(steamPath, "steamapps") };

        // 2. Parse libraryfolders.vdf for external libraries
        var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdfPath))
        {
            var lines = File.ReadAllLines(vdfPath);
            foreach (var line in lines)
            {
                // Simple search for "path" "..."
                var match = KeyValueRegex().Match(line);
                if (match.Success && match.Groups["key"].Value.Equals("path", StringComparison.OrdinalIgnoreCase))
                {
                    // Escape sequence fix if needed, usually cleaner in VDF
                    var path = match.Groups["value"].Value.Replace("\\\\", "\\");
                    var appsPath = Path.Combine(path, "steamapps");
                    if (Directory.Exists(appsPath))
                    {
                        libraryFolders.Add(appsPath);
                    }
                }
            }
        }

        // 3. Scan each library for appmanifest_*.acf
        foreach (var lib in libraryFolders.Distinct())
        {
            if (!Directory.Exists(lib)) continue;

            foreach (var file in Directory.GetFiles(lib, "appmanifest_*.acf"))
            {
                try
                {
                    var (name, appId, installDir) = ParseAppManifest(file);
                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(appId))
                    {
                        // Check if it's a "tool" or "game". Usually we just take everything.
                        // Can filter common tools if needed (e.g. Steamworks Common Redistributables appid 228980)
                        if (appId == "228980" || name.Contains("Steamworks Common")) continue;

                        games.Add((name, appId, installDir, lib));
                    }
                }
                catch 
                { 
                    // corrupted manifest? skip
                }
            }
        }

        return games;
    }

    private static (string Name, string AppId, string InstallDir) ParseAppManifest(string path)
    {
        string name = "", appId = "", installDir = "";
        foreach (var line in File.ReadLines(path))
        {
            var match = KeyValueRegex().Match(line);
            if (!match.Success) continue;

            var key = match.Groups["key"].Value.ToLowerInvariant();
            var val = match.Groups["value"].Value;

            switch (key)
            {
                case "name": name = val; break;
                case "appid": appId = val; break;
                case "installdir": installDir = val; break;
            }
        }
        return (name, appId, installDir);
    }

    private static string GetSteamPath()
    {
        // Try Registry 64-bit
        var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Valve\Steam");
        if (key?.GetValue("InstallPath") is string path && Directory.Exists(path)) return path;

        // Try Registry 32-bit
        key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
        if (key?.GetValue("InstallPath") is string path2 && Directory.Exists(path2)) return path2;

        return string.Empty;
    }
}
