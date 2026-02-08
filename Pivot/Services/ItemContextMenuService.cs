using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pivot.Engine.Models;
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

                // メニューアイテムを追加
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

