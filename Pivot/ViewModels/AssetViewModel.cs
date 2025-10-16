using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Pivot.Models;
using Pivot.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System; // StringComparison を使用するために追加

namespace Pivot.ViewModels
{
    public class AssetViewModel : ObservableObject
    {
        private readonly ILogger<AssetViewModel> _logger;
        private readonly MetadataService _metadataService;
        private readonly SettingsService _settingsService;

        public ObservableCollection<FileEntry> Assets { get; } = new ObservableCollection<FileEntry>();

        public IAsyncRelayCommand LoadAssetsCommand { get; }

        public AssetViewModel(
            ILogger<AssetViewModel> logger,
            MetadataService metadataService,
            SettingsService settingsService)
        {
            _logger = logger;
            _metadataService = metadataService;
            _settingsService = settingsService;

            LoadAssetsCommand = new AsyncRelayCommand(LoadAssetsAsync);
            _ = LoadAssetsAsync(); // 初期ロード
        }

        private async Task LoadAssetsAsync()
        {
            Assets.Clear();
            var settings = _settingsService.GetUserSettings();
            var assetPaths = settings.AssetDirectories;

            if (!assetPaths.Any()) return;

            var allFiles = await _metadataService.GetFilesAsync(0, int.MaxValue); // 全ファイル取得
            foreach (var file in allFiles.Where(f => IsAssetFile(f.Path, settings)))
            {
                Assets.Add(file);
            }
        }

        private bool IsAssetFile(string filePath, UserSettings settings)
        {
            // Assetディレクトリに含まれるファイルであるか
            bool inAssetDir = settings.AssetDirectories.Any(dir => filePath.StartsWith(dir, StringComparison.OrdinalIgnoreCase));
            if (!inAssetDir) return false;

            // ここでさらにAssetとして適切なMIMEタイプかフィルタリングすることも可能
            // 例: return inAssetDir && !filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
            return inAssetDir; // 現時点ではディレクトリに含まれるもの全てをAssetとする
        }
    }
}
