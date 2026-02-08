using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Pivot.Engine.Core;
using Pivot.Engine.Data;
using System.Drawing;
using System.Drawing.Imaging;

namespace Pivot.Engine.Jobs;

public class ShellThumbnailJob : Job
{
    private readonly string _sourcePath;
    private readonly string _destinationPath;
    private readonly int _width;
    private readonly int _height;
    private readonly DbContextOptions<PivotDbContext> _dbContextOptions;

    public ShellThumbnailJob(string id, string sourcePath, string destinationPath, int width, int height, DbContextOptions<PivotDbContext> dbContextOptions)
        : base(id)
    {
        _sourcePath = sourcePath;
        _destinationPath = destinationPath;
        _width = width;
        _height = height;
        _dbContextOptions = dbContextOptions;
    }

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        // Shell API integration might need STA thread. 
        // JobScheduler runs on thread pool which is MTA by default.
        // IShellItemImageFactory is usually fine on MTA for read-only, but to be safe we might wrap in a Task with STA if needed.
        // For now, we'll try running directly. If it fails, we'll need a dedicated STA thread job runner.
        
        await Task.Run(async () =>
        {
            if (TryCreateThumbnailFromShell(_sourcePath, _width, _height, _destinationPath))
            {
                using var db = new PivotDbContext(_dbContextOptions);
                var metadata = await db.AssetMetadata.FirstOrDefaultAsync(a => a.FilePath == _sourcePath, ct);
                if (metadata != null)
                {
                    metadata.ThumbnailPath = _destinationPath;
                    await db.SaveChangesAsync(ct);
                }
                else
                {
                    db.AssetMetadata.Add(new AssetMetadata
                    {
                        FilePath = _sourcePath,
                        ThumbnailPath = _destinationPath,
                        LastModifiedTicks = File.GetLastWriteTimeUtc(_sourcePath).Ticks,
                        FileSizeBytes = new FileInfo(_sourcePath).Length
                    });
                    await db.SaveChangesAsync(ct);
                }
            }
        }, ct);
    }

    private static bool TryCreateThumbnailFromShell(string sourcePath, int width, int height, string destinationPngPath)
    {
        try
        {
            Guid shellItemGuid = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"); // IShellItem
            Guid imageFactoryGuid = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"); // IShellItemImageFactory
            
            IntPtr shellItemPtr = IntPtr.Zero;
            IntPtr imageFactoryPtr = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;

            try
            {
                int hr = SHCreateItemFromParsingName(sourcePath, IntPtr.Zero, shellItemGuid, out shellItemPtr);
                if (hr != 0 || shellItemPtr == IntPtr.Zero) return false;

                hr = Marshal.QueryInterface(shellItemPtr, ref imageFactoryGuid, out imageFactoryPtr);
                if (hr != 0 || imageFactoryPtr == IntPtr.Zero) return false;

                var size = new SIZE { cx = width, cy = height };
                uint flags = 0x00000001; // SIIGBF_THUMBNAILONLY
                hr = IShellItemImageFactory_GetImage(imageFactoryPtr, ref size, flags, out hBitmap);
                if (hr != 0 || hBitmap == IntPtr.Zero) return false;

                using var bmp = System.Drawing.Image.FromHbitmap(hBitmap);
                // System.Drawing is only supported on Windows
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPngPath)!);
                bmp.Save(destinationPngPath, System.Drawing.Imaging.ImageFormat.Png);
                return File.Exists(destinationPngPath);
            }
            finally
            {
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                if (imageFactoryPtr != IntPtr.Zero) Marshal.Release(imageFactoryPtr);
                if (shellItemPtr != IntPtr.Zero) Marshal.Release(shellItemPtr);
            }
        }
        catch { return false; }
    }

    #region Native Shell API
    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        out IntPtr ppv);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private static int IShellItemImageFactory_GetImage(IntPtr pImageFactory, ref SIZE size, uint flags, out IntPtr phbm)
    {
        phbm = IntPtr.Zero;
        try
        {
            IntPtr vtable = Marshal.ReadIntPtr(pImageFactory);
            IntPtr getImagePtr = Marshal.ReadIntPtr(vtable, 3 * IntPtr.Size);
            var getImageDelegate = Marshal.GetDelegateForFunctionPointer<GetImageDelegate>(getImagePtr);
            return getImageDelegate(pImageFactory, ref size, flags, out phbm);
        }
        catch { return -1; }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetImageDelegate(IntPtr pImageFactory, ref SIZE size, uint flags, out IntPtr phbm);
    #endregion
}
