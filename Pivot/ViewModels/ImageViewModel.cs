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

namespace Pivot.ViewModels
{
    public class ImageViewModel : ObservableObject, IRecipient<DirectoryChangedMessage>
    {
        //private readonly ILogger<ImageViewModel> _logger; // コメントアウト
        private readonly MetadataService _metadataService;
        private readonly SettingsService _settingsService;
        private readonly IMessenger _messenger; // IMessenger を追加

        public ObservableCollection<FileEntry> Images { get; } = new ObservableCollection<FileEntry>();

        public IAsyncRelayCommand LoadImagesCommand { get; }

        public ImageViewModel(
            //ILogger<ImageViewModel> logger, // コメントアウト
            MetadataService metadataService,
            SettingsService settingsService,
            IMessenger messenger) // コンストラクタに IMessenger を追加
        {
            //_logger = logger; // コメントアウト
            _metadataService = metadataService;
            _settingsService = settingsService;
            _messenger = messenger; // 初期化

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

            var allFiles = await _metadataService.GetFilesAsync(0, int.MaxValue); // 全ファイル取得
            foreach (var file in allFiles.Where(f => IsImageFile(f.Path, settings)))
            {
                Images.Add(file);
            }
        }

        private bool IsImageFile(string filePath, UserSettings settings)
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
