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

namespace Pivot.ViewModels
{
    public class DirectoryViewModel : ObservableObject
    {
        //private readonly ILogger<DirectoryViewModel> _logger; // コメントアウト
        private readonly SettingsService? _settingsService;

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
                if (!GetDirectoryCollection(category).Contains(path))
                {
                    await _settingsService.AddDirectoryAsync(category, path);
                    GetDirectoryCollection(category).Add(path);
                    //_logger.LogInformation("Added directory: {Category} - {Path}", category, path); // コメントアウト
                }
            }
        }

        private async Task RemoveDirectoryAsync(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || _settingsService == null) return;

            DirectoryCategory? category = null;
            if (AssetDirectories.Contains(path)) category = DirectoryCategory.Asset;
            else if (ImageDirectories.Contains(path)) category = DirectoryCategory.Image;
            else if (ProjectDirectories.Contains(path)) category = DirectoryCategory.Project;

            if (category.HasValue)
            {
                await _settingsService.RemoveDirectoryAsync(category.Value, path);
                GetDirectoryCollection(category.Value).Remove(path);
                //_logger.LogInformation("Removed directory: {Category} - {Path}", category.Value, path); // コメントアウト
            }
            else
            {
                //_logger.LogWarning("Attempted to remove path not found in any category: {Path}", path); // コメントアウト
            }
        }

        // Asset ディレクトリ追加コマンド
        private async Task AddAssetDirectoryAsync(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || _settingsService == null) return;

            if (!AssetDirectories.Contains(path))
            {
                await _settingsService.AddDirectoryAsync(DirectoryCategory.Asset, path);
                AssetDirectories.Add(path);
                //_logger.LogInformation("Added Asset directory: {Path}", path); // コメントアウト
            }
        }

        // Image ディレクトリ追加コマンド
        private async Task AddImageDirectoryAsync(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || _settingsService == null) return;

            if (!ImageDirectories.Contains(path))
            {
                await _settingsService.AddDirectoryAsync(DirectoryCategory.Image, path);
                ImageDirectories.Add(path);
                //_logger.LogInformation("Added Image directory: {Path}", path); // コメントアウト
            }
        }

        // Project ディレクトリ追加コマンド
        private async Task AddProjectDirectoryAsync(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || _settingsService == null) return;

            if (!ProjectDirectories.Contains(path))
            {
                await _settingsService.AddDirectoryAsync(DirectoryCategory.Project, path);
                ProjectDirectories.Add(path);
                //_logger.LogInformation("Added Project directory: {Path}", path); // コメントアウト
            }
        }

        // Asset ディレクトリ選択コマンド（FolderPickerで選択）
        private async Task SelectAssetDirectoryAsync()
        {
            if (_settingsService == null) return;

            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            folderPicker.FileTypeFilter.Add("*");

            var uiWindow = App.Current.MainWindow as Microsoft.UI.Xaml.Window;
            if (uiWindow == null) return;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(uiWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null && !AssetDirectories.Contains(folder.Path))
            {
                await _settingsService.AddDirectoryAsync(DirectoryCategory.Asset, folder.Path);
                AssetDirectories.Add(folder.Path);
            }
        }

        // Image ディレクトリ選択コマンド（FolderPickerで選択）
        private async Task SelectImageDirectoryAsync()
        {
            if (_settingsService == null) return;

            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            folderPicker.FileTypeFilter.Add("*");

            var uiWindow = App.Current.MainWindow as Microsoft.UI.Xaml.Window;
            if (uiWindow == null) return;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(uiWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null && !ImageDirectories.Contains(folder.Path))
            {
                await _settingsService.AddDirectoryAsync(DirectoryCategory.Image, folder.Path);
                ImageDirectories.Add(folder.Path);
            }
        }

        // Project ディレクトリ選択コマンド（FolderPickerで選択）
        private async Task SelectProjectDirectoryAsync()
        {
            if (_settingsService == null) return;

            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            folderPicker.FileTypeFilter.Add("*");

            var uiWindow = App.Current.MainWindow as Microsoft.UI.Xaml.Window;
            if (uiWindow == null) return;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(uiWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null && !ProjectDirectories.Contains(folder.Path))
            {
                await _settingsService.AddDirectoryAsync(DirectoryCategory.Project, folder.Path);
                ProjectDirectories.Add(folder.Path);
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
