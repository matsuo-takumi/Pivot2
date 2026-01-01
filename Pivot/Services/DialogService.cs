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
    }
}
