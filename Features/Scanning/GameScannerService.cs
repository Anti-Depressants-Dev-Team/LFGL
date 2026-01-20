using System.Collections.Frozen;
using System.IO;
using Microsoft.Win32;

namespace LFGL.Features.Scanning;

public class GameScannerService : IGameScannerService
{
    // Launchers to EXCLUDE from results (we only want games, not the launcher clients)
    // NOTE: Minecraft launchers (Lunar, Prism, Feather) are kept since users want those
    private static readonly FrozenSet<string> ExcludedLaunchers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Steam", "Steam Client", "Steam.exe",
        "Epic Games Launcher", "Epic Games", "EpicGamesLauncher",
        "Hoyoplay", "HoYoPlay", "HYP",
        "itch", "itch.io", "Itch", 
        "GameJolt", "GameJolt Client", "Game Jolt",
        "GOG Galaxy", "Ubisoft Connect", "Origin", "EA App",
        "Battle.net", "Blizzard Battle.net"
    }.ToFrozenSet();

    public async Task<List<GameModel>> ScanAsync()
    {
        return await Task.Run(() =>
        {
            var results = new List<GameModel>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddGame(string name, string path, string originalPath)
            {
                if (string.IsNullOrWhiteSpace(path) || !seenPaths.Add(path)) return;
                
                // Skip launcher clients - we only want games
                if (IsExcludedLauncher(name)) return;
                
                var visualPath = IconExtractor.ExtractAndCacheIcon(originalPath, name) 
                    ?? "ms-appx:///Assets/Square44x44Logo.targetsize-24_altform-unplated.png";
                
                results.Add(new GameModel(name, path, visualPath));
            }

            // 1. Steam Integration (Deep Scan for actual games)
            var steamGames = SteamLibraryScanner.ScanSteamLibraries();
            foreach (var (sName, sAppId, sInstallDir, sLibPath) in steamGames)
            {
                // Launch Path: steam://run/{appid}
                var launchPath = $"steam://run/{sAppId}";
                
                // Icon Path Attempt:
                // Scan the game folder for the largest .exe or matching name .exe?
                var gameRoot = Path.Combine(sLibPath, "common", sInstallDir);
                string bestExe = "";
                
                if (Directory.Exists(gameRoot))
                {
                    // Heuristic: Try to find an exe that matches name or just take the largest one
                    var exes = Directory.GetFiles(gameRoot, "*.exe", SearchOption.TopDirectoryOnly);
                    
                    if (exes.Length > 0)
                    {
                        // 1. Try exact match loosely
                        bestExe = exes.FirstOrDefault(e => Path.GetFileNameWithoutExtension(e).Replace(" ", "").Contains(sInstallDir.Replace(" ", ""), StringComparison.OrdinalIgnoreCase)) ?? "";
                        
                        // 2. If nothing, take largest exe (usually the game, not the launcher/uninstaller)
                        if (string.IsNullOrEmpty(bestExe))
                        {
                            bestExe = exes.OrderByDescending(e => new FileInfo(e).Length).First();
                        }
                    }
                }

                // If no exe found, fallback to Steam exe itself? Or just let IconExtractor fail gracefully to placeholder.
                // We'll pass the bestExe we found for icon extraction. If empty, it returns null.
                
                // For Steam: Try to fetch high-quality artwork from Steam CDN
                var visualPath = FetchSteamArtwork(sAppId, sName) ?? IconExtractor.ExtractAndCacheIcon(bestExe, sName) ?? "ms-appx:///Assets/Square44x44Logo.targetsize-24_altform-unplated.png";
                results.Add(new GameModel(sName, launchPath, visualPath));
            }

            // 5. Desktop & Start Menu Shortcuts
            // We do this LAST so we don't add "Counter-Strike 2.lnk" if we already found the real Steam ID via scan.
            // But we need to handle duplicates logic.
            // Let's rely on Names for dedupe now? Or just let them be?
            // A simple name dedupe might be nice.
            
            var existingNames = results.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var shortcuts = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
            };

            foreach (var folder in shortcuts)
            {
                if (!Directory.Exists(folder)) continue;
                
                var lnkFiles = Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories);
                foreach (var lnk in lnkFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(lnk);
                    
                    // Skip if we already have this game (e.g. from Steam scan)
                    if (existingNames.Contains(fileName)) continue;

                    if (IsRelevantGame(fileName))
                    {
                        AddGame(fileName, lnk, lnk);
                    }
                }
            }

            return results;
        });
    }

    private static bool IsExcludedLauncher(string name)
    {
        // Check exact match first
        if (ExcludedLaunchers.Contains(name)) return true;
        
        // Check partial matches for common launcher patterns
        var lowerName = name.ToLowerInvariant();
        return lowerName.Contains("launcher") && 
               (lowerName.Contains("epic") || lowerName.Contains("steam") || 
                lowerName.Contains("hoyo") || lowerName.Contains("itch") || 
                lowerName.Contains("gamejolt") || lowerName.Contains("gog") ||
                lowerName.Contains("ubisoft") || lowerName.Contains("origin") ||
                lowerName.Contains("battle.net"));
    }

    private static bool IsRelevantGame(string name)
    {
        // Games we want to find from desktop/start menu shortcuts
        var targets = new[] { 
            "League", "Valorant", "Genshin", "Honkai", "Star Rail", "Zenless",
            "Minecraft", "Roblox", "Fortnite", "Apex", "Warzone", "CS2", "Counter-Strike",
            "Overwatch", "Diablo", "World of Warcraft", "Hearthstone",
            "GTA", "Red Dead", "Cyberpunk", "Witcher",
            "Elden Ring", "Dark Souls", "Hollow Knight", "Celeste",
            "Among Us", "Fall Guys", "Rocket League",
            // Minecraft launchers (users want these)
            "Lunar", "Prism", "Feather", "MultiMC", "ATLauncher", "Curseforge", "Modrinth"
        };
        
        // Skip if it's a launcher
        if (IsExcludedLauncher(name)) return false;
        
        return targets.Any(t => name.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly string ArtworkCacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LFGL", "SteamArt");

    private static string? FetchSteamArtwork(string appId, string gameName)
    {
        try
        {
            if (!Directory.Exists(ArtworkCacheDir)) Directory.CreateDirectory(ArtworkCacheDir);

            var safeName = string.Join("_", gameName.Split(Path.GetInvalidFileNameChars()));
            var cachePath = Path.Combine(ArtworkCacheDir, $"{safeName}_{appId}.jpg");

            if (File.Exists(cachePath)) return cachePath;

            // Steam CDN URL for header capsule (460x215, high quality)
            var headerUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg";

            using var client = new System.Net.Http.HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            
            var response = client.GetAsync(headerUrl).GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                File.WriteAllBytes(cachePath, bytes);
                return cachePath;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to fetch Steam art for {gameName}: {ex.Message}");
        }
        return null;
    }

    private static bool TryFindRegistryInstall(string[] publisherKeywords, out string exePath)
    {
        exePath = string.Empty;
        string[] registryKeys = [
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        ];

        foreach (var keyPath in registryKeys)
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            if (key == null) continue;

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                using var subKey = key.OpenSubKey(subKeyName);
                if (subKey == null) continue;

                var publisher = subKey.GetValue("Publisher") as string;
                var displayName = subKey.GetValue("DisplayName") as string;
                var displayIcon = subKey.GetValue("DisplayIcon") as string;
                var installLocation = subKey.GetValue("InstallLocation") as string;

                bool match = false;
                if (!string.IsNullOrEmpty(publisher) && publisherKeywords.Any(k => publisher.Contains(k, StringComparison.OrdinalIgnoreCase))) match = true;
                if (!string.IsNullOrEmpty(displayName) && publisherKeywords.Any(k => displayName.Contains(k, StringComparison.OrdinalIgnoreCase))) match = true;

                if (match)
                {
                    if (!string.IsNullOrEmpty(displayIcon) && displayIcon.EndsWith(".exe"))
                    {
                        exePath = displayIcon;
                        return true;
                    }
                    if (!string.IsNullOrEmpty(installLocation))
                    {
                         var files = Directory.GetFiles(installLocation, "*.exe");
                         if (files.Length > 0)
                         {
                             exePath = files[0];
                             return true;
                         }
                    }
                }
            }
        }
        return false;
    }
}
