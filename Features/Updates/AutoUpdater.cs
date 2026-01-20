using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace LFGL.Features.Updates;

public class AutoUpdater
{
    private const string UpdateCheckUrl = "https://api.github.com/repos/Anti-Depressants-Dev-Team/LFGL/releases/latest";
    private const string CurrentVersion = "1.0.0";
    
    public record UpdateInfo(string Version, string DownloadUrl, string ReleaseNotes);

    public static async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "LFGL-Updater");
            client.Timeout = TimeSpan.FromSeconds(10);

            var response = await client.GetStringAsync(UpdateCheckUrl);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "";
            var body = root.GetProperty("body").GetString() ?? "";
            
            // Get the first .exe asset
            string downloadUrl = "";
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                        break;
                    }
                }
            }

            // Compare versions
            if (IsNewerVersion(tagName, CurrentVersion))
            {
                return new UpdateInfo(tagName, downloadUrl, body);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
        }

        return null;
    }

    private static bool IsNewerVersion(string remote, string current)
    {
        try
        {
            var remoteParts = remote.Split('.').Select(int.Parse).ToArray();
            var currentParts = current.Split('.').Select(int.Parse).ToArray();

            for (int i = 0; i < Math.Min(remoteParts.Length, currentParts.Length); i++)
            {
                if (remoteParts[i] > currentParts[i]) return true;
                if (remoteParts[i] < currentParts[i]) return false;
            }
            return remoteParts.Length > currentParts.Length;
        }
        catch
        {
            return false;
        }
    }

    public static async Task DownloadAndInstallAsync(string downloadUrl, Action<int> progressCallback)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "LFGL_Update.exe");
        
        using var client = new HttpClient();
        using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        var buffer = new byte[8192];
        var bytesRead = 0L;

        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = File.Create(tempPath);

        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read));
            bytesRead += read;
            
            if (totalBytes > 0)
            {
                progressCallback((int)(bytesRead * 100 / totalBytes));
            }
        }

        // Launch installer and exit
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = tempPath,
            UseShellExecute = true
        };
        System.Diagnostics.Process.Start(psi);
        Environment.Exit(0);
    }
}
