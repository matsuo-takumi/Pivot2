using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Pivot.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System;
using Pivot.Models;
using System.IO;

namespace Pivot.ViewModels
{
    public class DirectoryViewModel : ObservableObject
    {
        private readonly DirectorySettingsService? _directorySettings;  
        private readonly IDialogService? _dialogService;

        private static string NormalizePath(string path)
        {
            try
            {
                var full = Path.GetFullPath(path ?? string.Empty);
                return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant();
            }
            catch
            {
                return (path ?? string.Empty).Trim().ToLowerInvariant();
            }
        }

        // Refresh ObservableCollections from DirectorySettingsService cache
        public void RefreshFromSettings()
        {
            if (_directorySettings == null) return;

            AssetDirectories.Clear();
            foreach (var d in _directorySettings.AssetDirectories)
            {
                AssetDirectories.Add(d);
            }

            ImageDirectories.Clear();
            foreach (var d in _directorySettings.ImageDirectories)
            {
                ImageDirectories.Add(d);
            }

            ProjectDirectories.Clear();
            foreach (var d in _directorySettings.ProjectDirectories)
            {
                ProjectDirectories.Add(d);
            }
        }

        // デザイン時用のコンストラクタ（XAMLデザイナーが使用）
        public DirectoryViewModel()
        {
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                AssetDirectories = new ObservableCollection<string> { "C:\\DesignMode\\Assets" };
                ImageDirectories = new ObservableCollection<string> { "C:\\DesignMode\\Images" };
                ProjectDirectories = new ObservableCollection<string> { "C:\\DesignMode\\Projects" };
                CodeDirectories = new ObservableCollection<string> { "C:\\DesignMode\\Code" };
                AddDirectoryCommand = new AsyncRelayCommand<DirectoryCategory>(async (_) => await Task.CompletedTask);
                RemoveDirectoryCommand = new AsyncRelayCommand<string>(async (_) => await Task.CompletedTask);
                AddAssetDirectoryCommand = new AsyncRelayCommand<string>(async (_) => await Task.CompletedTask);
                AddImageDirectoryCommand = new AsyncRelayCommand<string>(async (_) => await Task.CompletedTask);
                AddProjectDirectoryCommand = new AsyncRelayCommand<string>(async (_) => await Task.CompletedTask);
                AddCodeDirectoryCommand = new AsyncRelayCommand<string>(async (_) => await Task.CompletedTask);
                SelectAssetDirectoryCommand = new AsyncRelayCommand(async () => await Task.CompletedTask);
                SelectImageDirectoryCommand = new AsyncRelayCommand(async () => await Task.CompletedTask);
                SelectProjectDirectoryCommand = new AsyncRelayCommand(async () => await Task.CompletedTask);
                SelectCodeDirectoryCommand = new AsyncRelayCommand(async () => await Task.CompletedTask);
                OpenDirectoryCommand = new AsyncRelayCommand<string>(async (_) => await Task.CompletedTask);
            }
            else
            {
                // 実行時にこのコンストラクタが呼ばれるべきではない
                // DIコンテナが引数付きのコンストラクタを使用する想定
                throw new InvalidOperationException("Parameterless constructor should not be called at runtime.");
            }
        }

        // 実行時用のコンストラクタ（DIコンテナが使用）
        public DirectoryViewModel(
            DirectorySettingsService directorySettings,
            IDialogService dialogService)
        {
            _directorySettings = directorySettings;
            _dialogService = dialogService;

            AssetDirectories = new ObservableCollection<string>(_directorySettings.AssetDirectories);
            ImageDirectories = new ObservableCollection<string>(_directorySettings.ImageDirectories);
            ProjectDirectories = new ObservableCollection<string>(_directorySettings.ProjectDirectories);
            CodeDirectories = new ObservableCollection<string>(_directorySettings.CodeDirectories);

            AddDirectoryCommand = new AsyncRelayCommand<DirectoryCategory>(AddDirectoryAsync);
            RemoveDirectoryCommand = new AsyncRelayCommand<string>(RemoveDirectoryAsync);
            AddAssetDirectoryCommand = new AsyncRelayCommand<string>(AddAssetDirectoryAsync);
            AddImageDirectoryCommand = new AsyncRelayCommand<string>(AddImageDirectoryAsync);
            AddProjectDirectoryCommand = new AsyncRelayCommand<string>(AddProjectDirectoryAsync);
            SelectAssetDirectoryCommand = new AsyncRelayCommand(SelectAssetDirectoryAsync);
            SelectImageDirectoryCommand = new AsyncRelayCommand(SelectImageDirectoryAsync);
            SelectProjectDirectoryCommand = new AsyncRelayCommand(SelectProjectDirectoryAsync);
            SelectCodeDirectoryCommand = new AsyncRelayCommand(SelectCodeDirectoryAsync);
            AddCodeDirectoryCommand = new AsyncRelayCommand<string>(AddCodeDirectoryAsync);
            OpenDirectoryCommand = new AsyncRelayCommand<string>(OpenDirectoryAsync);
        }

        public ObservableCollection<string> AssetDirectories { get; }
        public ObservableCollection<string> ImageDirectories { get; }
        public ObservableCollection<string> ProjectDirectories { get; }
        public ObservableCollection<string> CodeDirectories { get; }

        public IAsyncRelayCommand AddDirectoryCommand { get; }
        public IAsyncRelayCommand<string> RemoveDirectoryCommand { get; }
        public IAsyncRelayCommand<string> AddAssetDirectoryCommand { get; }
        public IAsyncRelayCommand<string> AddImageDirectoryCommand { get; }
        public IAsyncRelayCommand<string> AddProjectDirectoryCommand { get; }
        public IAsyncRelayCommand<string> AddCodeDirectoryCommand { get; }

        // フォルダピッカーでディレクトリ選択するコマンド
        public IAsyncRelayCommand SelectAssetDirectoryCommand { get; }
        public IAsyncRelayCommand SelectImageDirectoryCommand { get; }
        public IAsyncRelayCommand SelectProjectDirectoryCommand { get; }
        public IAsyncRelayCommand SelectCodeDirectoryCommand { get; }

        // ディレクトリをエクスプローラーで開くコマンド
        public IAsyncRelayCommand<string> OpenDirectoryCommand { get; }

        private async Task AddDirectoryAsync(DirectoryCategory category)
        {
            if (_directorySettings == null) return;

            // TODO: FolderPicker を使用してディレクトリを選択させる
            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            folderPicker.FileTypeFilter.Add("*");
            
            // WinUI 3 の場合、ウィンドウハンドルを設定する必要がある
            var uiWindow = App.Current.MainWindow as Microsoft.UI.Xaml.Window;
            if (uiWindow == null) return; // UI Windowが取得できない場合は処理を中断

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(uiWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                string path = folder.Path;
                if (!GetDirectoryCollection(category).Contains(NormalizePath(path)))
                {
                    await _directorySettings.AddDirectoryAsync(category, path);
                    GetDirectoryCollection(category).Add(NormalizePath(path));
                    //_logger.LogInformation("Added directory: {Category} - {Path}", category, path); // コメントアウト
                }
            }
        }

        // Asset ディレクトリ選択コマンド（IDialogServiceで選択）
        private async Task SelectAssetDirectoryAsync()
        {
            if (_directorySettings == null || _dialogService == null) return;
            await SelectDirectoryForCategoryAsync(DirectoryCategory.Asset, AssetDirectories);
        }

        // Image ディレクトリ選択コマンド（IDialogServiceで選択）
        private async Task SelectImageDirectoryAsync()
        {
            if (_directorySettings == null || _dialogService == null) return;
            await SelectDirectoryForCategoryAsync(DirectoryCategory.Image, ImageDirectories);
        }

        // Project ディレクトリ選択コマンド（IDialogServiceで選択）
        private async Task SelectProjectDirectoryAsync()
        {
            if (_directorySettings == null || _dialogService == null) return;
            await SelectDirectoryForCategoryAsync(DirectoryCategory.Project, ProjectDirectories);
        }

        // Code ディレクトリ選択コマンド（IDialogServiceで選択）
        private async Task SelectCodeDirectoryAsync()
        {
            if (_directorySettings == null || _dialogService == null) return;
            await SelectDirectoryForCategoryAsync(DirectoryCategory.Code, CodeDirectories);
        }

        /// <summary>
        /// 共通のフォルダ選択ロジック。IDialogServiceを使用。
        /// </summary>
        private async Task SelectDirectoryForCategoryAsync(DirectoryCategory category, ObservableCollection<string> collection)
        {
            try
            {
                var selectedPath = await _dialogService!.PickFolderAsync();
                if (string.IsNullOrEmpty(selectedPath)) return;

                var normalized = NormalizePath(selectedPath);
                if (!collection.Any(d => NormalizePath(d) == normalized))
                {
                    await _directorySettings!.AddDirectoryAsync(category, selectedPath);
                    collection.Add(selectedPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SelectDirectoryForCategoryAsync error: {ex.Message}");
            }
        }

        private async Task RemoveDirectoryAsync(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || _directorySettings == null) return;

            DirectoryCategory? category = null;
            string normalizedPathToRemove = NormalizePath(path);
            
            System.Diagnostics.Debug.WriteLine($"[Debug] Remove: PathToRemove='{path}', Normalized='{normalizedPathToRemove}'");

            if (AssetDirectories.Any(d => NormalizePath(d) == normalizedPathToRemove))
            {
                category = DirectoryCategory.Asset;
                System.Diagnostics.Debug.WriteLine($"[Debug] Remove: Found in AssetDirectories.");
            }
            else if (ImageDirectories.Any(d => NormalizePath(d) == normalizedPathToRemove))
            {
                category = DirectoryCategory.Image;
                System.Diagnostics.Debug.WriteLine($"[Debug] Remove: Found in ImageDirectories.");
            }
            else if (ProjectDirectories.Any(d => NormalizePath(d) == normalizedPathToRemove))
            {
                category = DirectoryCategory.Project;
                System.Diagnostics.Debug.WriteLine($"[Debug] Remove: Found in ProjectDirectories.");
            }

            if (category.HasValue)
            {
                System.Diagnostics.Debug.WriteLine($"[Debug] Remove: Attempting to remove '{path}' from Category {category.Value}.");
                await _directorySettings.RemoveDirectoryAsync(category.Value, path);
                // ObservableCollection からも正規化されたパスで削除
                var collection = GetDirectoryCollection(category.Value);
                var itemToRemove = collection.FirstOrDefault(d => NormalizePath(d) == normalizedPathToRemove);
                if (itemToRemove != null)
                {
                    collection.Remove(itemToRemove);
                    System.Diagnostics.Debug.WriteLine($"[Debug] Remove: Successfully removed '{itemToRemove}'. Current count: {collection.Count}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Debug] Remove: Item '{path}' not found in ObservableCollection after SettingsService removal.");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[Debug] Remove: Path '{path}' not found in any category.");
            }
        }

        // Asset ディレクトリ追加コマンド
        private async Task AddAssetDirectoryAsync(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || _directorySettings == null) return;

            if (!AssetDirectories.Contains(NormalizePath(path)))
            {
                await _directorySettings.AddDirectoryAsync(DirectoryCategory.Asset, path);
                AssetDirectories.Add(NormalizePath(path));
                //_logger.LogInformation("Added Asset directory: {Path}", path); // コメントアウト
            }
        }

        // Image ディレクトリ追加コマンド
        private async Task AddImageDirectoryAsync(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || _directorySettings == null) return;

            if (!ImageDirectories.Contains(NormalizePath(path)))
            {
                await _directorySettings.AddDirectoryAsync(DirectoryCategory.Image, path);
                ImageDirectories.Add(NormalizePath(path));
                //_logger.LogInformation("Added Image directory: {Path}", path); // コメントアウト
            }
        }

        // Project ディレクトリ追加コマンド
        private async Task AddProjectDirectoryAsync(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || _directorySettings == null) return;

            if (!ProjectDirectories.Contains(NormalizePath(path)))
            {
                await _directorySettings.AddDirectoryAsync(DirectoryCategory.Project, path);
                ProjectDirectories.Add(NormalizePath(path));
                //_logger.LogInformation("Added Project directory: {Path}", path); // コメントアウト
            }
        }

        // Code ディレクトリ追加コマンド
        private async Task AddCodeDirectoryAsync(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || _directorySettings == null) return;

            if (!CodeDirectories.Contains(NormalizePath(path)))
            {
                await _directorySettings.AddDirectoryAsync(DirectoryCategory.Code, path);
                CodeDirectories.Add(NormalizePath(path));
            }
        }

        private ObservableCollection<string> GetDirectoryCollection(DirectoryCategory category)
        {
            return category switch
            {
                DirectoryCategory.Asset => AssetDirectories,
                DirectoryCategory.Image => ImageDirectories,
                DirectoryCategory.Project => ProjectDirectories,
                DirectoryCategory.Code => CodeDirectories,
                _ => throw new ArgumentOutOfRangeException(nameof(category))
            };
        }

        // ディレクトリをエクスプローラーで開く
        private async Task OpenDirectoryAsync(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                // Windowsの explorer.exe でディレクトリを開く
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                };

                using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                    await Task.CompletedTask;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OpenDirectoryAsync error: {ex.Message}");
            }
        }
    }
}
