using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Pivot.Services
{
    /// <summary>
    /// 再利用可能なドラッグ&ドロップサービス
    /// ファイルやアイテムをドラッグして別の場所にコピーできるようにする
    /// </summary>
    public class DragDropService
    {
        /// <summary>
        /// DragStartingイベントハンドラーを設定する
        /// </summary>
        /// <param name="element">ドラッグ可能なUI要素</param>
        /// <param name="items">ドラッグするアイテムのリスト</param>
        /// <param name="getFilePath">アイテムからファイルパスを取得する関数</param>
        public static void SetupDragStarting(UIElement element, IEnumerable<object> items, Func<object, string?> getFilePath)
        {
            element.CanDrag = true;
            element.DragStarting += async (sender, e) =>
            {
                try
                {
                    if (items == null || !items.Any()) return;

                    var filePaths = items.Select(getFilePath).Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path)).Select(p => p!).ToList();
                    if (filePaths.Count == 0) return;

                    var storageItems = await CreateStorageItems(filePaths);
                    if (storageItems.Count == 0) return;

                    e.Data.SetStorageItems(storageItems);
                    e.Data.RequestedOperation = DataPackageOperation.Copy;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DragDropService.DragStarting error: {ex.Message}");
                }
            };
        }

        /// <summary>
        /// ファイルパスのリストからStorageItemsを作成（公開メソッド）
        /// </summary>
        public static async Task<IReadOnlyList<IStorageItem>> CreateStorageItems(List<string> filePaths)
        {
            var storageItems = new List<IStorageItem>();
            foreach (var path in filePaths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        var file = await StorageFile.GetFileFromPathAsync(path);
                        storageItems.Add(file);
                    }
                    else if (Directory.Exists(path))
                    {
                        var folder = await StorageFolder.GetFolderFromPathAsync(path);
                        storageItems.Add(folder);
                    }
                }
                catch
                {
                    // ファイルにアクセスできない場合はスキップ
                }
            }
            return storageItems;
        }

        /// <summary>
        /// ドロップイベントを処理する
        /// </summary>
        /// <param name="sender">ドロップを受け取るUI要素</param>
        /// <param name="e">DragEventArgs</param>
        /// <param name="onFilesDropped">ファイルがドロップされたときに呼ばれるコールバック</param>
        public static async void HandleDrop(FrameworkElement sender, DragEventArgs e, Func<IReadOnlyList<string>, Task> onFilesDropped)
        {
            try
            {
                if (e.DataView == null) return;

                if (e.DataView.Contains(StandardDataFormats.StorageItems))
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    var filePaths = items.OfType<StorageFile>().Select(f => f.Path).ToList();
                    
                    if (filePaths.Count > 0 && onFilesDropped != null)
                    {
                        await onFilesDropped(filePaths);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DragDropService.HandleDrop error: {ex.Message}");
            }
        }

        /// <summary>
        /// ドラッグオーバーイベントを処理する（ドロップ可能かどうかを示す）
        /// </summary>
        public static void HandleDragOver(FrameworkElement sender, DragEventArgs e)
        {
            try
            {
                if (e.DataView == null) return;

                if (e.DataView.Contains(StandardDataFormats.StorageItems))
                {
                    e.AcceptedOperation = DataPackageOperation.Copy;
                    e.DragUIOverride.Caption = "ファイルをコピー";
                }
                else
                {
                    e.AcceptedOperation = DataPackageOperation.None;
                }
            }
            catch
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        }
    }
}

