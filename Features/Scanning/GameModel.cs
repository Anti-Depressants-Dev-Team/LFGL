namespace LFGL.Features.Scanning;

public readonly record struct GameModel(
    string Name,
    string ExecutablePath,
    string IconPath,
    string Category = "All",
    bool IsManual = false
);
