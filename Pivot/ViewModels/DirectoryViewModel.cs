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
        private readonly SettingsService _settingsService;

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
        }

        public ObservableCollection<string> AssetDirectories { get; }
        public ObservableCollection<string> ImageDirectories { get; }
        public ObservableCollection<string> ProjectDirectories { get; }

        public IAsyncRelayCommand AddDirectoryCommand { get; }
        public IAsyncRelayCommand<string> RemoveDirectoryCommand { get; }

        private async Task AddDirectoryAsync(DirectoryCategory category)
        {
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

        private async Task RemoveDirectoryAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

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
