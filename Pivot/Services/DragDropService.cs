using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;
using Pivot.Engine.Models;
using Pivot.Models;

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
                        System.Diagnostics.Debug.WriteLine($"DragDropService: Successfully created StorageFile for {path}");
                    }
                    else if (Directory.Exists(path))
                    {
                        var folder = await StorageFolder.GetFolderFromPathAsync(path);
                        storageItems.Add(folder);
                        System.Diagnostics.Debug.WriteLine($"DragDropService: Successfully created StorageFolder for {path}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"DragDropService: Path does not exist: {path}");
                    }
                }
                catch (Exception ex)
                {
                    // ファイルにアクセスできない場合はスキップ
                    System.Diagnostics.Debug.WriteLine($"DragDropService: Failed to create StorageItem for {path}: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"  Exception type: {ex.GetType().Name}");
                }
            }
            System.Diagnostics.Debug.WriteLine($"DragDropService: Created {storageItems.Count} storage item(s) from {filePaths.Count} path(s)");
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

        /// <summary>
        /// TemplateItem用のドラッグ開始処理（カスタムプレビュー付き）
        /// ファイルカードで表示するすべてのタブで利用可能
        /// </summary>
        /// <param name="sender">ドラッグ開始するUI要素</param>
        /// <param name="e">DragStartingEventArgs</param>
        /// <param name="currentItem">現在のアイテム（ドラッグされたカードのDataContext）</param>
        /// <param name="selectedItems">選択されているアイテムのコレクション（複数選択対応）</param>
        public static async Task HandleDragStartingForTemplateItem(
            UIElement sender, 
            DragStartingEventArgs e, 
            TemplateItem currentItem, 
            IEnumerable<object> selectedItems)
        {
            System.Diagnostics.Debug.WriteLine("DragDropService.HandleDragStartingForTemplateItem: Event fired!");
            try
            {
                if (currentItem == null)
                {
                    System.Diagnostics.Debug.WriteLine("DragDropService: currentItem is null");
                    return;
                }

                // 選択されているアイテムを取得（選択されていない場合は現在のアイテムのみ）
                var itemsToDrag = selectedItems != null && selectedItems.Any() 
                    ? selectedItems.Cast<TemplateItem>() 
                    : new[] { currentItem };

                System.Diagnostics.Debug.WriteLine($"DragDropService: Attempting to drag {itemsToDrag.Count()} item(s)");

                // ファイルパスを取得
                var filePaths = itemsToDrag
                    .Select(i => i.Path)
                    .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    .ToList();

                if (filePaths.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("DragDropService: No valid file paths found");
                    return;
                }

                foreach (var path in filePaths)
                {
                    System.Diagnostics.Debug.WriteLine($"DragDropService:   - File: {path}");
                }

                // StorageItemsを作成
                var storageItems = await CreateStorageItems(filePaths);
                if (storageItems.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("DragDropService: Failed to create storage items");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"DragDropService: Created {storageItems.Count} storage item(s)");

                // StorageItemsを設定（readOnly: falseでファイルをコピー可能にする）
                e.Data.SetStorageItems(storageItems, readOnly: false);
                System.Diagnostics.Debug.WriteLine($"DragDropService: SetStorageItems called with {storageItems.Count} item(s), readOnly=false");

                // コピー操作を要求
                e.Data.RequestedOperation = DataPackageOperation.Copy;
                System.Diagnostics.Debug.WriteLine($"DragDropService: RequestedOperation set to {e.Data.RequestedOperation}");

                // カスタムドラッグプレビュー: 画像のサムネイルを表示
                await SetCustomDragPreview(e, itemsToDrag.FirstOrDefault());

                System.Diagnostics.Debug.WriteLine("DragDropService: Drag started successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DragDropService.HandleDragStartingForTemplateItem error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                e.Cancel = true; // エラー時はドラッグをキャンセル
            }
        }
        
        /// <summary>
        /// AssetEntity用のドラッグ開始処理
        /// UnifiedBrowserControlで使用
        /// </summary>
        /// <param name="sender">ドラッグ開始するUI要素</param>
        /// <param name="e">DragStartingEventArgs</param>
        /// <param name="currentItem">現在のアイテム</param>
        /// <param name="selectedItems">選択されているアイテムのコレクション</param>
        public static async Task HandleDragStartingForAssetEntity(
            UIElement sender, 
            DragStartingEventArgs e, 
            AssetEntity currentItem, 
            IEnumerable<AssetEntity> selectedItems)
        {
            System.Diagnostics.Debug.WriteLine("DragDropService.HandleDragStartingForAssetEntity: Event fired!");
            try
            {
                if (currentItem == null)
                {
                    System.Diagnostics.Debug.WriteLine("DragDropService: currentItem is null");
                    return;
                }

                // 選択されているアイテムを取得（選択されていない場合は現在のアイテムのみ）
                var itemsToDrag = selectedItems != null && selectedItems.Any() 
                    ? selectedItems 
                    : new[] { currentItem };

                System.Diagnostics.Debug.WriteLine($"DragDropService: Attempting to drag {itemsToDrag.Count()} AssetEntity item(s)");

                // ファイルパスを取得
                var filePaths = itemsToDrag
                    .Select(i => i.FilePath)
                    .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    .ToList();

                if (filePaths.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("DragDropService: No valid file paths found");
                    return;
                }

                foreach (var path in filePaths)
                {
                    System.Diagnostics.Debug.WriteLine($"DragDropService:   - File: {path}");
                }

                // StorageItemsを作成
                var storageItems = await CreateStorageItems(filePaths);
                if (storageItems.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("DragDropService: Failed to create storage items");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"DragDropService: Created {storageItems.Count} storage item(s)");

                // StorageItemsを設定（readOnly: falseでファイルをコピー可能にする）
                e.Data.SetStorageItems(storageItems, readOnly: false);
                System.Diagnostics.Debug.WriteLine($"DragDropService: SetStorageItems called with {storageItems.Count} item(s), readOnly=false");

                // コピー操作を要求
                e.Data.RequestedOperation = DataPackageOperation.Copy;
                System.Diagnostics.Debug.WriteLine($"DragDropService: RequestedOperation set to {e.Data.RequestedOperation}");

                // カスタムドラッグプレビュー: 画像のサムネイルを表示
                var firstItem = itemsToDrag.FirstOrDefault();
                if (firstItem != null && File.Exists(firstItem.FilePath))
                {
                    try
                    {
                        var imageFile = await StorageFile.GetFileFromPathAsync(firstItem.FilePath);
                        var stream = await imageFile.OpenReadAsync();
                        var bitmapImage = new BitmapImage();
                        bitmapImage.DecodePixelWidth = 200;
                        await bitmapImage.SetSourceAsync(stream);
                        e.DragUI.SetContentFromBitmapImage(bitmapImage);
                        System.Diagnostics.Debug.WriteLine("DragDropService: Set custom drag preview with image");
                    }
                    catch (Exception previewEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"DragDropService: Failed to set preview: {previewEx.Message}");
                        e.DragUI.SetContentFromDataPackage();
                    }
                }
                else
                {
                    e.DragUI.SetContentFromDataPackage();
                }

                System.Diagnostics.Debug.WriteLine("DragDropService: AssetEntity drag started successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DragDropService.HandleDragStartingForAssetEntity error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                e.Cancel = true;
            }
        }

        /// <summary>
        /// カスタムドラッグプレビューを設定（画像のサムネイルを表示）
        /// </summary>
        private static async Task SetCustomDragPreview(DragStartingEventArgs e, TemplateItem? item)
        {
            try
            {
                if (item == null)
                {
                    e.DragUI.SetContentFromDataPackage();
                    System.Diagnostics.Debug.WriteLine("DragDropService: Using default drag preview (no item)");
                    return;
                }

                BitmapImage? bitmapImage = null;

                // ThumbnailPathから画像を読み込む
                if (!string.IsNullOrWhiteSpace(item.ThumbnailPath))
                {
                    try
                    {
                        // URI形式の場合（file:/// や http:// など）
                        if (Uri.TryCreate(item.ThumbnailPath, UriKind.Absolute, out var thumbnailUri))
                        {
                            bitmapImage = new BitmapImage(thumbnailUri);
                            bitmapImage.DecodePixelWidth = 200; // ドラッグプレビュー用にサイズを制限
                            System.Diagnostics.Debug.WriteLine($"DragDropService: Created BitmapImage from URI: {item.ThumbnailPath}");
                        }
                        // ファイルパスの場合
                        else if (File.Exists(item.ThumbnailPath))
                        {
                            var thumbnailFile = await StorageFile.GetFileFromPathAsync(item.ThumbnailPath);
                            var stream = await thumbnailFile.OpenReadAsync();
                            bitmapImage = new BitmapImage();
                            bitmapImage.DecodePixelWidth = 200;
                            await bitmapImage.SetSourceAsync(stream);
                            System.Diagnostics.Debug.WriteLine($"DragDropService: Created BitmapImage from file: {item.ThumbnailPath}");
                        }
                    }
                    catch (Exception thumbEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"DragDropService: Failed to load thumbnail: {thumbEx.Message}");
                    }
                }

                // サムネイルが利用できない場合は、元の画像ファイルから読み込む
                if (bitmapImage == null && File.Exists(item.Path))
                {
                    try
                    {
                        var imageFile = await StorageFile.GetFileFromPathAsync(item.Path);
                        var stream = await imageFile.OpenReadAsync();
                        bitmapImage = new BitmapImage();
                        bitmapImage.DecodePixelWidth = 200; // ドラッグプレビュー用にサイズを制限
                        await bitmapImage.SetSourceAsync(stream);
                        System.Diagnostics.Debug.WriteLine($"DragDropService: Created BitmapImage from original image: {item.Path}");
                    }
                    catch (Exception imgEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"DragDropService: Failed to load original image: {imgEx.Message}");
                    }
                }

                // BitmapImageが作成できた場合は、カスタムプレビューを設定
                if (bitmapImage != null)
                {
                    e.DragUI.SetContentFromBitmapImage(bitmapImage);
                    System.Diagnostics.Debug.WriteLine("DragDropService: Set custom drag preview with image");
                }
                else
                {
                    // フォールバック: システムデフォルトを使用
                    e.DragUI.SetContentFromDataPackage();
                    System.Diagnostics.Debug.WriteLine("DragDropService: Using default drag preview (fallback)");
                }
            }
            catch (Exception previewEx)
            {
                // プレビュー設定に失敗した場合は、システムデフォルトを使用
                System.Diagnostics.Debug.WriteLine($"DragDropService: Failed to set custom preview: {previewEx.Message}");
                e.DragUI.SetContentFromDataPackage();
            }
        }
    }
}

