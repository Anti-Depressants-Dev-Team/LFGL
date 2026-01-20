using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace LFGL.Features.Scanning;

public static class IconExtractor
{
    private static readonly string CacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LFGL", "IconCache");

    // Shell32 for large icons (up to 256x256)
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;      // 32x32
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
    
    // For jumbo icons (256x256), we use IShellItemImageFactory
    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage([In, MarshalAs(UnmanagedType.Struct)] SIZE size, [In] SIIGBF flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
        public SIZE(int cx, int cy) { this.cx = cx; this.cy = cy; }
    }

    [Flags]
    private enum SIIGBF
    {
        SIIGBF_RESIZETOFIT = 0x00,
        SIIGBF_BIGGERSIZEOK = 0x01,
        SIIGBF_MEMORYONLY = 0x02,
        SIIGBF_ICONONLY = 0x04,
        SIIGBF_THUMBNAILONLY = 0x08,
        SIIGBF_INCACHEONLY = 0x10,
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        [In, MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        [In] IntPtr pbc,
        [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [Out, MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    public static string? ExtractAndCacheIcon(string exePath, string gameName)
    {
        try
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return null;

            if (!Directory.Exists(CacheDir))
            {
                Directory.CreateDirectory(CacheDir);
            }

            var safeName = string.Join("_", gameName.Split(Path.GetInvalidFileNameChars()));
            var cachePath = Path.Combine(CacheDir, $"{safeName}.png");

            if (File.Exists(cachePath))
            {
                return cachePath;
            }

            // Try to get a large (256x256) icon using Shell
            using var bitmap = GetLargeIcon(exePath, 256);
            if (bitmap != null)
            {
                bitmap.Save(cachePath, ImageFormat.Png);
                return cachePath;
            }

            // Fallback to standard extraction
            using var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon != null)
            {
                using var bmp = icon.ToBitmap();
                bmp.Save(cachePath, ImageFormat.Png);
                return cachePath;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to extract icon for {gameName}: {ex.Message}");
            return null;
        }
    }

    private static Bitmap? GetLargeIcon(string path, int size)
    {
        try
        {
            var iidImageFactory = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");
            SHCreateItemFromParsingName(path, IntPtr.Zero, iidImageFactory, out var factory);

            var sz = new SIZE(size, size);
            factory.GetImage(sz, SIIGBF.SIIGBF_BIGGERSIZEOK | SIIGBF.SIIGBF_ICONONLY, out var hBitmap);
            
            if (hBitmap != IntPtr.Zero)
            {
                var bmp = System.Drawing.Image.FromHbitmap(hBitmap);
                DeleteObject(hBitmap);
                return bmp;
            }
        }
        catch
        {
            // Silently fail, fallback will be used
        }
        return null;
    }
}
