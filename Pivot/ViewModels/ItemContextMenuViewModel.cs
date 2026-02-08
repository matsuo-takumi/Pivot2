using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pivot.Engine.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace Pivot.ViewModels
{
    /// <summary>
    /// アイテムカードの右クリックメニューを管理するViewModel
    /// </summary>
    public partial class ItemContextMenuViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _directoryPath = string.Empty;

        [ObservableProperty]
        private string _fileName = string.Empty;

        [ObservableProperty]
        private string _fullPath = string.Empty;

        private TemplateItem? _item;

        public ItemContextMenuViewModel()
        {
        }

        /// <summary>
        /// アイテムを設定してメニュー情報を更新
        /// </summary>
        public void SetItem(TemplateItem item)
        {
            _item = item;
            FullPath = item.Path ?? string.Empty;
            
            if (!string.IsNullOrWhiteSpace(FullPath))
            {
                try
                {
                    DirectoryPath = Path.GetDirectoryName(FullPath) ?? string.Empty;
                    FileName = Path.GetFileName(FullPath);
                }
                catch
                {
                    DirectoryPath = string.Empty;
                    FileName = FullPath;
                }
            }
            else
            {
                DirectoryPath = string.Empty;
                FileName = string.Empty;
            }
        }

        /// <summary>
        /// エクスプローラーでフォルダを開く
        /// </summary>
        [RelayCommand]
        private async Task OpenContainingFolderAsync()
        {
            if (string.IsNullOrWhiteSpace(FullPath)) return;

            try
            {
                var file = await StorageFile.GetFileFromPathAsync(FullPath);
                var folder = await file.GetParentAsync();
                
                if (folder != null)
                {
                    await Windows.System.Launcher.LaunchFolderAsync(folder);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open containing folder: {ex.Message}");
            }
        }
    }
}

