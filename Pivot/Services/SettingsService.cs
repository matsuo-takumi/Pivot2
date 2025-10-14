using Microsoft.Extensions.Configuration;
using Pivot.Models;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System; // AppContext を使用するために追加

namespace Pivot.Services
{
    public class SettingsService
    {
        private readonly IConfiguration _configuration;
        private readonly string _settingsFilePath;
        private DirectorySettings _directorySettings;

        public SettingsService(IConfiguration configuration)
        {
            _configuration = configuration;
            // appsettings.json とは別に、ユーザー設定を保存するファイルを想定
            _settingsFilePath = Path.Combine(AppContext.BaseDirectory, "user_settings.json");
            _directorySettings = LoadSettings();
        }

        public DirectorySettings GetDirectorySettings() => _directorySettings;

        public async Task AddDirectoryAsync(DirectoryCategory category, string path)
        {
            switch (category)
            {
                case DirectoryCategory.Asset:
                    _directorySettings.AssetDirectories.Add(path);
                    break;
                case DirectoryCategory.Image:
                    _directorySettings.ImageDirectories.Add(path);
                    break;
                case DirectoryCategory.Project:
                    _directorySettings.ProjectDirectories.Add(path);
                    break;
            }
            await SaveSettingsAsync();
        }

        public async Task RemoveDirectoryAsync(DirectoryCategory category, string path)
        {
            switch (category)
            {
                case DirectoryCategory.Asset:
                    _directorySettings.AssetDirectories.Remove(path);
                    break;
                case DirectoryCategory.Image:
                    _directorySettings.ImageDirectories.Remove(path);
                    break;
                case DirectoryCategory.Project:
                    _directorySettings.ProjectDirectories.Remove(path);
                    break;
            }
            await SaveSettingsAsync();
        }

        private DirectorySettings LoadSettings()
        {
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    return JsonSerializer.Deserialize<DirectorySettings>(json) ?? new DirectorySettings();
                }
                catch (JsonException /* ex */)
                {
                    // ロギングを追加すべきだが、ここでは簡略化
                    return new DirectorySettings();
                }
            }
            return new DirectorySettings();
        }

        private async Task SaveSettingsAsync()
        {
            string json = JsonSerializer.Serialize(_directorySettings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsFilePath, json);
        }
    }

    public enum DirectoryCategory
    {
        Asset,
        Image,
        Project
    }
}
