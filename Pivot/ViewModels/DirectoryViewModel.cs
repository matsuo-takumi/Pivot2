using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Pivot.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System; // ArgumentOutOfRangeException を使用するために追加
using Windows.Storage.Pickers; // FolderPicker を使用するために追加
using Microsoft.Extensions.DependencyInjection; // GetService 拡張メソッドを使用するために追加
using Pivot.Models;
using System.IO;

namespace Pivot.ViewModels
{
    public class DirectoryViewModel : ObservableObject
    {
        //private readonly ILogger<DirectoryViewModel> _logger; // コメントアウト
        private readonly SettingsService? _settingsService;

        // Dynamic sections for UI
        public ObservableCollection<DirectorySection> Sections { get; }

        public class DirectorySection
        {
            public string Title { get; set; } = string.Empty;
            public ObservableCollection<string> Items { get; set; } = new ObservableCollection<string>();
            public IAsyncRelayCommand? AddCommand { get; set; }
            public IAsyncRelayCommand<string>? RemoveCommand { get; set; }
        }

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

        // Refresh ObservableCollections from SettingsService cache
        public void RefreshFromSettings()
        {
            if (_settingsService == null) return;
            var settings = _settingsService.GetUserSettings();

            AssetDirectories.Clear();
            foreach (var d in settings.AssetDirectories)
            {
                AssetDirectories.Add(d);
            }

            ImageDirectories.Clear();
            foreach (var d in settings.ImageDirectories)
            {
                ImageDirectories.Add(d);
            }

            ProjectDirectories.Clear();
            foreach (var d in settings.ProjectDirectories)
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
                AddDirectoryCommand = new AsyncRelayCommand<DirectoryCategory>(async (_) => await Task.CompletedTask);
                RemoveDirectoryCommand = new AsyncRelayCommand<string>(async (_) => await Task.CompletedTask);
                AddAssetDirectoryCommand = new AsyncRelayCommand<string>(async (_) => await Task.CompletedTask);
                AddImageDirectoryCommand = new AsyncRelayCommand<string>(async (_) => await Task.CompletedTask);
                AddProjectDirectoryCommand = new AsyncRelayCommand<string>(async (_) => await Task.CompletedTask);
                SelectAssetDirectoryCommand = new AsyncRelayCommand(async () => await Task.CompletedTask);
                SelectImageDirectoryCommand = new AsyncRelayCommand(async () => await Task.CompletedTask);
                SelectProjectDirectoryCommand = new AsyncRelayCommand(async () => await Task.CompletedTask);
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
            //ILogger<DirectoryViewModel> logger, // コメントアウト
            SettingsService settingsService)
        {
            //_logger = logger; // コメントアウト
            _settingsService = settingsService;

            var settings = _settingsService.GetUserSettings();
            AssetDirectories = new ObservableCollection<string>(settings.AssetDirectories);
            ImageDirectories = new ObservableCollection<string>(settings.ImageDirectories);
            ProjectDirectories = new ObservableCollection<string>(settings.ProjectDirectories);

            AddDirectoryCommand = new AsyncRelayCommand<DirectoryCategory>(AddDirectoryAsync);
            RemoveDirectoryCommand = new AsyncRelayCommand<string>(RemoveDirectoryAsync);
            AddAssetDirectoryCommand = new AsyncRelayCommand<string>(AddAssetDirectoryAsync);
            AddImageDirectoryCommand = new AsyncRelayCommand<string>(AddImageDirectoryAsync);
            AddProjectDirectoryCommand = new AsyncRelayCommand<string>(AddProjectDirectoryAsync);
            SelectAssetDirectoryCommand = new AsyncRelayCommand(SelectAssetDirectoryAsync);
            SelectImageDirectoryCommand = new AsyncRelayCommand(SelectImageDirectoryAsync);
            SelectProjectDirectoryCommand = new AsyncRelayCommand(SelectProjectDirectoryAsync);

            // Initialize dynamic sections mapping to collections and commands
            Sections = new ObservableCollection<DirectorySection>
            {
                new DirectorySection
                {
                    Title = "Asset Directories",
                    Items = AssetDirectories,
                    AddCommand = SelectAssetDirectoryCommand,
                    RemoveCommand = RemoveDirectoryCommand
                },
                new DirectorySection
                {
                    Title = "Image Directories",
                    Items = ImageDirectories,
                    AddCommand = SelectImageDirectoryCommand,
                    RemoveCommand = RemoveDirectoryCommand
                },
                new DirectorySection
                {
                    Title = "Project Directories",
                    Items = ProjectDirectories,
                    AddCommand = SelectProjectDirectoryCommand,
                    RemoveCommand = RemoveDirectoryCommand
                }
            };
        }

        public ObservableCollection<string> AssetDirectories { get; }
        public ObservableCollection<string> ImageDirectories { get; }
        public ObservableCollection<string> ProjectDirectories { get; }

        public IAsyncRelayCommand AddDirectoryCommand { get; }
        public IAsyncRelayCommand<string> RemoveDirectoryCommand { get; }
        public IAsyncRelayCommand<string> AddAssetDirectoryCommand { get; }
        public IAsyncRelayCommand<string> AddImageDirectoryCommand { get; }
        public IAsyncRelayCommand<string> AddProjectDirectoryCommand { get; }

        // フォルダピッカーでディレクトリ選択するコマンド
        public IAsyncRelayCommand SelectAssetDirectoryCommand { get; }
        public IAsyncRelayCommand SelectImageDirectoryCommand { get; }
        public IAsyncRelayCommand SelectProjectDirectoryCommand { get; }

        private async Task AddDirectoryAsync(DirectoryCategory category)
        {
            if (_settingsService == null) return;

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
                    await _settingsService.AddDirectoryAsync(category, path);
                    GetDirectoryCollection(category).Add(NormalizePath(path));
                    //_logger.LogInformation("Added directory: {Category} - {Path}", category, path); // コメントアウト
                }
            }
        }

        // Asset ディレクトリ選択コマンド（FolderPickerで選択）
        private async Task SelectAssetDirectoryAsync()
        {
            if (_settingsService == null) return;

            try
            {
                var folderPicker = new Windows.Storage.Pickers.FolderPicker();
                folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
                folderPicker.FileTypeFilter.Add("*");

                var uiWindow = App.Current.MainWindow as Microsoft.UI.Xaml.Window;
                if (uiWindow == null) return;

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(uiWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    string selectedPath = folder.Path; // 選択された元のパス
                    string normalizedSelectedPath = NormalizePath(selectedPath);
                    
                    System.Diagnostics.Debug.WriteLine($"[Debug] SelectAsset: SelectedPath='{selectedPath}', Normalized='{normalizedSelectedPath}'");
                    System.Diagnostics.Debug.WriteLine($"[Debug] SelectAsset: Current AssetDirectories normalized: {string.Join(", ", AssetDirectories.Select(NormalizePath))}");

                    if (!AssetDirectories.Any(d => NormalizePath(d) == normalizedSelectedPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"[Debug] SelectAsset: Adding '{selectedPath}' to settings and UI.");
                        await _settingsService.AddDirectoryAsync(DirectoryCategory.Asset, selectedPath);
                        AssetDirectories.Add(selectedPath);
                        System.Diagnostics.Debug.WriteLine($"[Debug] SelectAsset: Added '{selectedPath}'. Current count: {AssetDirectories.Count}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Debug] SelectAsset: '{selectedPath}' already exists (normalized). Not adding.");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SelectAssetDirectoryAsync error: {ex.Message}");
            }
        }

        // Image ディレクトリ選択コマンド（FolderPickerで選択）
        private async Task SelectImageDirectoryAsync()
        {
            if (_settingsService == null) return;

            try
            {
                var folderPicker = new Windows.Storage.Pickers.FolderPicker();
                folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
                folderPicker.FileTypeFilter.Add("*");

                var uiWindow = App.Current.MainWindow as Microsoft.UI.Xaml.Window;
                if (uiWindow == null) return;

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(uiWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    string selectedPath = folder.Path;
                    string normalizedSelectedPath = NormalizePath(selectedPath);
                    
                    System.Diagnostics.Debug.WriteLine($"[Debug] SelectImage: SelectedPath='{selectedPath}', Normalized='{normalizedSelectedPath}'");
                    System.Diagnostics.Debug.WriteLine($"[Debug] SelectImage: Current ImageDirectories normalized: {string.Join(", ", ImageDirectories.Select(NormalizePath))}");

                    if (!ImageDirectories.Any(d => NormalizePath(d) == normalizedSelectedPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"[Debug] SelectImage: Adding '{selectedPath}' to settings and UI.");
                        await _settingsService.AddDirectoryAsync(DirectoryCategory.Image, selectedPath);
                        ImageDirectories.Add(selectedPath);
                        System.Diagnostics.Debug.WriteLine($"[Debug] SelectImage: Added '{selectedPath}'. Current count: {ImageDirectories.Count}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Debug] SelectImage: '{selectedPath}' already exists (normalized). Not adding.");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SelectImageDirectoryAsync error: {ex.Message}");
            }
        }

        // Project ディレクトリ選択コマンド（FolderPickerで選択）
        private async Task SelectProjectDirectoryAsync()
        {
            if (_settingsService == null) return;

            try
            {
                var folderPicker = new Windows.Storage.Pickers.FolderPicker();
                folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
                folderPicker.FileTypeFilter.Add("*");

                var uiWindow = App.Current.MainWindow as Microsoft.UI.Xaml.Window;
                if (uiWindow == null) return;

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(uiWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    string selectedPath = folder.Path;
                    string normalizedSelectedPath = NormalizePath(selectedPath);
                    
                    System.Diagnostics.Debug.WriteLine($"[Debug] SelectProject: SelectedPath='{selectedPath}', Normalized='{normalizedSelectedPath}'");
                    System.Diagnostics.Debug.WriteLine($"[Debug] SelectProject: Current ProjectDirectories normalized: {string.Join(", ", ProjectDirectories.Select(NormalizePath))}");

                    if (!ProjectDirectories.Any(d => NormalizePath(d) == normalizedSelectedPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"[Debug] SelectProject: Adding '{selectedPath}' to settings and UI.");
                        await _settingsService.AddDirectoryAsync(DirectoryCategory.Project, selectedPath);
                        ProjectDirectories.Add(selectedPath);
                        System.Diagnostics.Debug.WriteLine($"[Debug] SelectProject: Added '{selectedPath}'. Current count: {ProjectDirectories.Count}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Debug] SelectProject: '{selectedPath}' already exists (normalized). Not adding.");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SelectProjectDirectoryAsync error: {ex.Message}");
            }
        }

        private async Task RemoveDirectoryAsync(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || _settingsService == null) return;

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
                await _settingsService.RemoveDirectoryAsync(category.Value, path);
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
            if (string.IsNullOrWhiteSpace(path) || _settingsService == null) return;

            if (!AssetDirectories.Contains(NormalizePath(path)))
            {
                await _settingsService.AddDirectoryAsync(DirectoryCategory.Asset, path);
                AssetDirectories.Add(NormalizePath(path));
                //_logger.LogInformation("Added Asset directory: {Path}", path); // コメントアウト
            }
        }

        // Image ディレクトリ追加コマンド
        private async Task AddImageDirectoryAsync(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || _settingsService == null) return;

            if (!ImageDirectories.Contains(NormalizePath(path)))
            {
                await _settingsService.AddDirectoryAsync(DirectoryCategory.Image, path);
                ImageDirectories.Add(NormalizePath(path));
                //_logger.LogInformation("Added Image directory: {Path}", path); // コメントアウト
            }
        }

        // Project ディレクトリ追加コマンド
        private async Task AddProjectDirectoryAsync(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || _settingsService == null) return;

            if (!ProjectDirectories.Contains(NormalizePath(path)))
            {
                await _settingsService.AddDirectoryAsync(DirectoryCategory.Project, path);
                ProjectDirectories.Add(NormalizePath(path));
                //_logger.LogInformation("Added Project directory: {Path}", path); // コメントアウト
            }
        }

        private ObservableCollection<string> GetDirectoryCollection(DirectoryCategory category)
        {
            return category switch
            {
                DirectoryCategory.Asset => AssetDirectories,
                DirectoryCategory.Image => ImageDirectories,
                DirectoryCategory.Project => ProjectDirectories,
                _ => throw new ArgumentOutOfRangeException(nameof(category))
            };
        }
    }
}
