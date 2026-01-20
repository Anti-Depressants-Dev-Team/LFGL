namespace LFGL.Features.Scanning;

public interface IGameScannerService
{
    Task<List<GameModel>> ScanAsync();
}
