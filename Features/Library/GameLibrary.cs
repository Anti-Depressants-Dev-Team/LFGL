using System.IO;
using System.Text.Json;
using LFGL.Features.Scanning;

namespace LFGL.Features.Library;

public class GameLibrary
{
    private static readonly string LibraryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
        "LFGL", "library.json");

    private List<GameModel> _games = [];
    private readonly object _lock = new();

    public IReadOnlyList<GameModel> Games => _games.AsReadOnly();

    public GameLibrary()
    {
        Load();
    }

    public void SetScannedGames(IEnumerable<GameModel> scannedGames)
    {
        lock (_lock)
        {
            // Keep manual games, replace scanned ones
            var manualGames = _games.Where(g => g.IsManual).ToList();
            _games = [.. scannedGames.Select(g => g with { IsManual = false }), .. manualGames];
            Save();
        }
    }

    public void AddManualGame(string name, string executablePath, string iconPath, string category = "Manual")
    {
        lock (_lock)
        {
            var game = new GameModel(name, executablePath, iconPath, category, IsManual: true);
            _games.Add(game);
            Save();
        }
    }

    public void RemoveGame(string executablePath)
    {
        lock (_lock)
        {
            _games.RemoveAll(g => g.ExecutablePath.Equals(executablePath, StringComparison.OrdinalIgnoreCase));
            Save();
        }
    }

    public IEnumerable<GameModel> GetByCategory(string category)
    {
        if (category == "All" || string.IsNullOrEmpty(category))
            return _games;
        
        return _games.Where(g => g.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
    }

    public void SetFavorite(string executablePath, bool isFavorite)
    {
        lock (_lock)
        {
            var idx = _games.FindIndex(g => g.ExecutablePath.Equals(executablePath, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                var game = _games[idx];
                _games[idx] = game with { Category = isFavorite ? "Favorites" : (game.IsManual ? "Manual" : "Steam") };
                Save();
            }
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(LibraryPath))
            {
                var json = File.ReadAllText(LibraryPath);
                _games = JsonSerializer.Deserialize<List<GameModel>>(json) ?? [];
            }
        }
        catch
        {
            _games = [];
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(LibraryPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_games, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(LibraryPath, json);
        }
        catch
        {
            // Silent fail for now
        }
    }
}
