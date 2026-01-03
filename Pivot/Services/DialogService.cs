using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace Pivot.Services
{
    /// <summary>
    /// IDialogServiceの実装。WinUI 3のファイルピッカーを使用する。
    /// </summary>
    public class DialogService : IDialogService
    {
        /// <summary>
        /// フォルダ選択ダイアログを表示する。
        /// </summary>
        public async Task<string?> PickFolderAsync()
        {
            try
            {
                var folderPicker = new FolderPicker();
                folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
                folderPicker.FileTypeFilter.Add("*");

                // WinUI 3ではウィンドウハンドルの設定が必要
                var window = App.Current.MainWindow;
                if (window == null) return null;

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

                var folder = await folderPicker.PickSingleFolderAsync();
                return folder?.Path;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DialogService.PickFolderAsync error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// ファイル選択ダイアログを表示する。
        /// </summary>
        public async Task<string?> PickFileAsync(IEnumerable<string>? extensions = null)
        {
            try
            {
                var filePicker = new FileOpenPicker();
                filePicker.SuggestedStartLocation = PickerLocationId.Desktop;

                if (extensions != null && extensions.Any())
                {
                    foreach (var ext in extensions)
                    {
                        filePicker.FileTypeFilter.Add(ext);
                    }
                }
                else
                {
                    filePicker.FileTypeFilter.Add("*");
                }

                var window = App.Current.MainWindow;
                if (window == null) return null;

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

                var file = await filePicker.PickSingleFileAsync();
                return file?.Path;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DialogService.PickFileAsync error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 複数ファイル選択ダイアログを表示する。
        /// </summary>
        public async Task<IReadOnlyList<string>> PickFilesAsync(IEnumerable<string>? extensions = null)
        {
            try
            {
                var filePicker = new FileOpenPicker();
                filePicker.SuggestedStartLocation = PickerLocationId.Desktop;

                if (extensions != null && extensions.Any())
                {
                    foreach (var ext in extensions)
                    {
                        filePicker.FileTypeFilter.Add(ext);
                    }
                }
                else
                {
                    filePicker.FileTypeFilter.Add("*");
                }

                var window = App.Current.MainWindow;
                if (window == null) return Array.Empty<string>();

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

                var files = await filePicker.PickMultipleFilesAsync();
                return files?.Select(f => f.Path).ToList() ?? new List<string>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DialogService.PickFilesAsync error: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// 複数フォルダ選択。Win32 IFileOpenDialog COM APIを使用。
        /// </summary>
        public async Task<IReadOnlyList<string>> PickMultipleFoldersAsync()
        {
            return await Task.Run(() =>
            {
                var selectedFolders = new List<string>();
                
                try
                {
                    var dialog = new NativeMethods.FileOpenDialogClass() as NativeMethods.IFileOpenDialog;
                    if (dialog == null) return selectedFolders;

                    // Set options: pick folders, allow multi-select
                    dialog.GetOptions(out uint options);
                    options |= NativeMethods.FOS_PICKFOLDERS | NativeMethods.FOS_ALLOWMULTISELECT | NativeMethods.FOS_FORCEFILESYSTEM;
                    dialog.SetOptions(options);

                    dialog.SetTitle("フォルダーを選択 (複数選択可)");

                    // Get window handle on UI thread
                    IntPtr hwnd = IntPtr.Zero;
                    App.Current.MainWindow?.DispatcherQueue.TryEnqueue(() =>
                    {
                        var window = App.Current.MainWindow;
                        if (window != null)
                        {
                            hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                        }
                    });
                    
                    // Small delay to ensure hwnd is set
                    System.Threading.Thread.Sleep(50);
                    
                    if (hwnd == IntPtr.Zero)
                    {
                        // Fallback: try to get hwnd directly
                        var window = App.Current.MainWindow;
                        if (window != null)
                        {
                            hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                        }
                    }

                    int hr = dialog.Show(hwnd);
                    if (hr != 0) return selectedFolders; // User cancelled or error

                    dialog.GetResults(out NativeMethods.IShellItemArray results);
                    if (results == null) return selectedFolders;

                    results.GetCount(out uint count);
                    for (uint i = 0; i < count; i++)
                    {
                        results.GetItemAt(i, out NativeMethods.IShellItem item);
                        if (item != null)
                        {
                            item.GetDisplayName(NativeMethods.SIGDN_FILESYSPATH, out string path);
                            if (!string.IsNullOrEmpty(path))
                            {
                                selectedFolders.Add(path);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DialogService.PickMultipleFoldersAsync error: {ex.Message}");
                }

                return selectedFolders;
            });
        }
    }

    /// <summary>
    /// Native methods for Win32 COM dialog
    /// </summary>
    internal static class NativeMethods
    {
        public const uint FOS_PICKFOLDERS = 0x00000020;
        public const uint FOS_ALLOWMULTISELECT = 0x00000200;
        public const uint FOS_FORCEFILESYSTEM = 0x00000040;
        public const uint SIGDN_FILESYSPATH = 0x80058000;

        [System.Runtime.InteropServices.ComImport]
        [System.Runtime.InteropServices.Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
        internal class FileOpenDialogClass { }

        [System.Runtime.InteropServices.ComImport]
        [System.Runtime.InteropServices.Guid("d57c7288-d4ad-4768-be02-9d969532d960")]
        [System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IFileOpenDialog
        {
            [System.Runtime.InteropServices.PreserveSig]
            int Show(IntPtr parent);
            void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
            void SetFileTypeIndex(uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise(IntPtr pfde, out uint pdwCookie);
            void Unadvise(uint dwCookie);
            void SetOptions(uint fos);
            void GetOptions(out uint pfos);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszName);
            void GetFileName([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] out string pszName);
            void SetTitle([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
            void AddPlace(IShellItem psi, int fdap);
            void SetDefaultExtension([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close(int hr);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr pFilter);
            void GetResults(out IShellItemArray ppenum);
            void GetSelectedItems(out IShellItemArray ppsai);
        }

        [System.Runtime.InteropServices.ComImport]
        [System.Runtime.InteropServices.Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        [System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        [System.Runtime.InteropServices.ComImport]
        [System.Runtime.InteropServices.Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
        [System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IShellItemArray
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppvOut);
            void GetPropertyStore(int flags, ref Guid riid, out IntPtr ppv);
            void GetPropertyDescriptionList(IntPtr keyType, ref Guid riid, out IntPtr ppv);
            void GetAttributes(int AttribFlags, uint sfgaoMask, out uint psfgaoAttribs);
            void GetCount(out uint pdwNumItems);
            void GetItemAt(uint dwIndex, out IShellItem ppsi);
            void EnumItems(out IntPtr ppenumShellItems);
        }
    }
}
