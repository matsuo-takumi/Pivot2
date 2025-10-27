using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Pivot.Models;
using Pivot.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System;
using CommunityToolkit.Mvvm.Messaging; // IMessenger を追加
using Pivot.Messages; // DirectoryChangedMessage を使用するために追加
using System.Threading; // SemaphoreSlim を追加

namespace Pivot.ViewModels
{
    public class ImageViewModel : ObservableObject, IRecipient<DirectoryChangedMessage>
    {
        //private readonly ILogger<ImageViewModel> _logger; // コメントアウト
        private readonly MetadataService _metadataService;
        private readonly SettingsService _settingsService;
        private readonly IMessenger _messenger; // IMessenger を追加
        private readonly IThumbnailService _thumbnailService;

        private static readonly int ThumbnailWidth = 220;
        private static readonly int ThumbnailHeight = 160;
        // サムネイル生成の並列度制御
        private readonly SemaphoreSlim _thumbnailParallelismSemaphore = new SemaphoreSlim(Math.Max(2, Environment.ProcessorCount / 2));

        public ObservableCollection<ImageItem> Images { get; } = new ObservableCollection<ImageItem>();

        public IAsyncRelayCommand LoadImagesCommand { get; }

        public ImageViewModel(
            //ILogger<ImageViewModel> logger, // コメントアウト
            MetadataService metadataService,
            SettingsService settingsService,
            IMessenger messenger,
            IThumbnailService thumbnailService) // コンストラクタに IMessenger / ThumbnailService を追加
        {
            //_logger = logger; // コメントアウト
            _metadataService = metadataService;
            _settingsService = settingsService;
            _messenger = messenger; // 初期化
            _thumbnailService = thumbnailService;

            LoadImagesCommand = new AsyncRelayCommand(LoadImagesAsync);
            _ = LoadImagesAsync(); // 初期ロード

            // DirectoryChangedMessage をリッスン
            _messenger.Register<ImageViewModel, DirectoryChangedMessage>(this, (r, m) => r.Handle(m));
        }

        public void Handle(DirectoryChangedMessage message)
        {
            if (message.Value.Category == DirectoryCategory.Image)
            {
                // Image ディレクトリが変更されたら画像を再ロード
                _ = LoadImagesAsync();
            }
        }

        // IRecipient<T> implementation required by CommunityToolkit
        public void Receive(DirectoryChangedMessage message) => Handle(message);

        private async Task LoadImagesAsync()
        {
            Images.Clear();
            var settings = _settingsService.GetUserSettings();
            var imagePaths = settings.ImageDirectories;

            if (!imagePaths.Any()) return;

            // サムネイルサービスを一度だけ初期化
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var cacheDir = System.IO.Path.Combine(local, "Pivot", "cache", "thumbnails");
            await _thumbnailService.InitializeAsync(cacheDir, 500L * 1024 * 1024);

            // 爆速表示: ファイルシステムから直接列挙し、キャッシュヒットを即表示
            var imageFiles = new System.Collections.Generic.List<string>();
            foreach (var dir in imagePaths)
            {
                try
                {
                    if (!System.IO.Directory.Exists(dir)) continue;
                    var files = System.IO.Directory.EnumerateFiles(dir, "*", System.IO.SearchOption.AllDirectories);
                    foreach (var p in files)
                    {
                        if (IsImageFile(p, settings)) imageFiles.Add(p);
                    }
                }
                catch { }
            }

            // 先にアイテムを追加（名前のみ即表示）
            foreach (var path in imageFiles)
            {
                var fi = new System.IO.FileInfo(path);
                Images.Add(new ImageItem
                {
                    Path = path,
                    Name = System.IO.Path.GetFileName(path),
                    Size = fi.Exists ? fi.Length : 0,
                    LastModified = fi.Exists ? fi.LastWriteTimeUtc : DateTime.MinValue,
                    ThumbnailPath = _thumbnailService.TryGetCachedThumbnailPath(path, ThumbnailWidth, ThumbnailHeight) // キャッシュがあれば即表示
                });
            }

            // キャッシュミス分を制限付き並列で生成
            var tasks = Images
                .Where(i => string.IsNullOrEmpty(i.ThumbnailPath))
                .Select(async item =>
                {
                    await _thumbnailParallelismSemaphore.WaitAsync();
                    try
                    {
                        var thumb = await _thumbnailService.GetOrCreateThumbnailAsync(item.Path, ThumbnailWidth, ThumbnailHeight);
                        item.ThumbnailPath = thumb;
                    }
                    catch { }
                    finally { _thumbnailParallelismSemaphore.Release(); }
                });

            _ = Task.Run(async () => await Task.WhenAll(tasks));
        }

        private bool IsImageFile(string filePath, UserSettings settings)
        {
            // Imageディレクトリに含まれるファイルであるか
            bool inImageDir = settings.ImageDirectories.Any(dir => filePath.StartsWith(dir, StringComparison.OrdinalIgnoreCase));
            if (!inImageDir) return false;

            // ここでさらにImageとして適切なMIMEタイプかフィルタリングすることも可能
            // FileScannerService.GuessMime メソッドで "image/*" と判定される拡張子を持つファイルであるか確認するなど
            // 例: return inImageDir && FileScannerService.IsImageExtension(System.IO.Path.GetExtension(filePath));
            // 一旦拡張子フィルタ（jpg/jpeg/png/webp/gif/bmp/ico/tif/tiff/tga）
            var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            switch (ext)
            {
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".webp":
                case ".gif":
                case ".bmp":
                case ".ico":
                case ".tif":
                case ".tiff":
                case ".tga":
                    return true;
                default:
                    return false;
            }
        }

        private async Task EnsureThumbnailAsync(ImageItem item)
        {
            try
            {
                var path = await _thumbnailService.GetOrCreateThumbnailAsync(item.Path, ThumbnailWidth, ThumbnailHeight);
                item.ThumbnailPath = path;
            }
            catch
            {
                // ignore; placeholder will be used
            }
        }
    }
}
