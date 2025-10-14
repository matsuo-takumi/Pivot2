using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Pivot.Models;
using Pivot.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Pivot.ViewModels
{
    public class ImageViewModel : ObservableObject
    {
        private readonly ILogger<ImageViewModel> _logger;
        private readonly MetadataService _metadataService;
        private readonly SettingsService _settingsService;

        public ObservableCollection<FileEntry> Images { get; } = new ObservableCollection<FileEntry>();

        public IAsyncRelayCommand LoadImagesCommand { get; }

        public ImageViewModel(
            ILogger<ImageViewModel> logger,
            MetadataService metadataService,
            SettingsService settingsService)
        {
            _logger = logger;
            _metadataService = metadataService;
            _settingsService = settingsService;

            LoadImagesCommand = new AsyncRelayCommand(LoadImagesAsync);
            _ = LoadImagesAsync(); // 初期ロード
        }

        private async Task LoadImagesAsync()
        {
            Images.Clear();
            var settings = _settingsService.GetDirectorySettings();
            var imagePaths = settings.ImageDirectories;

            if (!imagePaths.Any()) return;

            var allFiles = await _metadataService.GetFilesAsync(0, int.MaxValue); // 全ファイル取得
            foreach (var file in allFiles.Where(f => IsImageFile(f.Path, settings)))
            {
                Images.Add(file);
            }
        }

        private bool IsImageFile(string filePath, DirectorySettings settings)
        {
            // Imageディレクトリに含まれるファイルであるか
            bool inImageDir = settings.ImageDirectories.Any(dir => filePath.StartsWith(dir, StringComparison.OrdinalIgnoreCase));
            if (!inImageDir) return false;

            // ここでさらにImageとして適切なMIMEタイプかフィルタリングすることも可能
            // FileScannerService.GuessMime メソッドで "image/*" と判定される拡張子を持つファイルであるか確認するなど
            // 例: return inImageDir && FileScannerService.IsImageExtension(System.IO.Path.GetExtension(filePath));
            return inImageDir; // 現時点ではディレクトリに含まれるもの全てをImageとする
        }
    }
}
