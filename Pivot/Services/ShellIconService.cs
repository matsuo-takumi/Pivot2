using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Pivot.Services
{
    public class ShellIconService : IDisposable
    {
        private readonly string _cacheDir;
        private readonly ConcurrentDictionary<string, string> _extToPath = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public ShellIconService(string cacheDir)
        {
            _cacheDir = cacheDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pivot", "cache", "thumbnails", "icons");
            Directory.CreateDirectory(_cacheDir);
        }

        public void Dispose() { }

        public Task<string?> GetOrCreateIconForExtensionAsync(string extension, int size, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(extension)) return Task.FromResult<string?>(null);
            var clean = extension.StartsWith(".") ? extension.Substring(1) : extension;
            var key = clean.ToLowerInvariant() + "_" + size + ".png";
            var path = Path.Combine(_cacheDir, key);
            if (File.Exists(path)) return Task.FromResult<string?>(path);
            return Task.Run(() => CreateIconForExtension(clean, size, path, ct), ct);
        }

        private string? CreateIconForExtension(string extNoDot, int size, string destinationPath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var fakeName = "dummy." + extNoDot;
                var shfi = new SHFILEINFO();
                var flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES | SHGFI_LARGEICON;
                IntPtr res = SHGetFileInfo(fakeName, FILE_ATTRIBUTE_NORMAL, out shfi, (uint)Marshal.SizeOf(shfi), flags);
                if (shfi.hIcon != IntPtr.Zero)
                {
                    try
                    {
                        using var icon = System.Drawing.Icon.FromHandle(shfi.hIcon);
                        using var bmp = icon.ToBitmap();
                        // resize if needed
                        using var final = new System.Drawing.Bitmap(bmp, Math.Max(1, size), Math.Max(1, size));
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                        final.Save(destinationPath, System.Drawing.Imaging.ImageFormat.Png);
                        return destinationPath;
                    }
                    finally
                    {
                        DestroyIcon(shfi.hIcon);
                    }
                }
            }
            catch { }
            return null;
        }

        #region Native
        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000; // large
        private const uint SHGFI_SMALLICON = 0x000000001; // small
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
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

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
        #endregion
    }
}


