using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Pivot.Models;
using System;
using System.IO;

namespace Pivot.Services
{
    /// <summary>
    /// アイテムカードの右クリックメニューを管理する共通サービス
    /// MVVMパターンに従い、再利用可能なモジュールとして実装
    /// </summary>
    public static class ItemContextMenuService
    {
        /// <summary>
        /// アイテム用の右クリックメニューを作成して設定
        /// </summary>
        /// <param name="element">メニューを設定するUI要素</param>
        /// <param name="item">表示するアイテム</param>
        public static void SetupContextMenu(FrameworkElement element, TemplateItem item)
        {
            if (element == null || item == null) return;

            try
            {
                var menuFlyout = new MenuFlyout();

                // ディレクトリパス表示（無効化）
                var directoryItem = new MenuFlyoutItem
                {
                    IsEnabled = false
                };

                // ファイル名表示（太字）
                var fileNameItem = new MenuFlyoutItem
                {
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold
                };

                // セパレーター
                var separator = new MenuFlyoutSeparator();

                // フォルダを開く
                var openFolderItem = new MenuFlyoutItem
                {
                    Text = "Open containing folder"
                };
                openFolderItem.Icon = new FontIcon
                {
                    Glyph = "\uE838",
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets")
                };
                openFolderItem.Click += (s, e) => OpenContainingFolder(item.Path);

                // パス情報を設定
                try
                {
                    var directoryPath = Path.GetDirectoryName(item.Path) ?? string.Empty;
                    var fileName = Path.GetFileName(item.Path);

                    directoryItem.Text = directoryPath;
                    fileNameItem.Text = fileName;
                }
                catch
                {
                    directoryItem.Text = string.Empty;
                    fileNameItem.Text = item.Path ?? string.Empty;
                }

                // メニューアイテムを追加
                menuFlyout.Items.Add(directoryItem);
                menuFlyout.Items.Add(fileNameItem);
                menuFlyout.Items.Add(separator);
                menuFlyout.Items.Add(openFolderItem);

                // ContextFlyoutを設定
                element.ContextFlyout = menuFlyout;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ItemContextMenuService.SetupContextMenu error: {ex.Message}");
            }
        }

        /// <summary>
        /// RightTappedイベントハンドラーでメニューを更新
        /// </summary>
        /// <param name="sender">イベント送信元</param>
        /// <param name="e">イベント引数</param>
        public static void HandleRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement element && element.DataContext is TemplateItem item)
                {
                    var border = element as Border;
                    if (border?.ContextFlyout is MenuFlyout menuFlyout)
                    {
                        // メニューアイテムを更新
                        if (menuFlyout.Items.Count >= 2)
                        {
                            var directoryItem = menuFlyout.Items[0] as MenuFlyoutItem;
                            var fileNameItem = menuFlyout.Items[1] as MenuFlyoutItem;

                            if (directoryItem != null && fileNameItem != null)
                            {
                                try
                                {
                                    var directoryPath = Path.GetDirectoryName(item.Path) ?? string.Empty;
                                    var fileName = Path.GetFileName(item.Path);

                                    directoryItem.Text = directoryPath;
                                    fileNameItem.Text = fileName;
                                }
                                catch
                                {
                                    directoryItem.Text = string.Empty;
                                    fileNameItem.Text = item.Path ?? string.Empty;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ItemContextMenuService.HandleRightTapped error: {ex.Message}");
            }
        }

        /// <summary>
        /// エクスプローラーでフォルダを開く
        /// </summary>
        private static void OpenContainingFolder(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            try
            {
                if (File.Exists(filePath))
                {
                    // エクスプローラーでファイルを選択してフォルダを開く
                    var processInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{filePath}\"",
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(processInfo);
                }
                else if (Directory.Exists(filePath))
                {
                    // ディレクトリの場合はそのまま開く
                    var processInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{filePath}\"",
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(processInfo);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ItemContextMenuService.OpenContainingFolder error: {ex.Message}");
            }
        }
    }
}

