using Microsoft.Extensions.Configuration;
using Pivot.Models;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System; // AppContext を使用するために追加
using Microsoft.UI.Xaml; // ElementThemeを使用するために追加
// using static Pivot.MainWindow; // BackdropType was moved out

namespace Pivot.Services
{
    public class SettingsService
    {
        private readonly IConfiguration _configuration;
        private readonly string _settingsFilePath;
        private UserSettings _userSettings;

        public SettingsService(IConfiguration configuration)
        {
            _configuration = configuration;
            // appsettings.json とは別に、ユーザー設定を保存するファイルを想定
            _settingsFilePath = Path.Combine(AppContext.BaseDirectory, "user_settings.json");
            _userSettings = LoadSettings();
        }

        public UserSettings GetUserSettings() => _userSettings;

        public ElementTheme GetTheme()
        {
            return _userSettings.AppTheme;
        }

        public async Task SetTheme(ElementTheme theme)
        {
            _userSettings.AppTheme = theme;
            await SaveSettingsAsync();
        }

        public BackdropType GetBackdropType()
        {
            return _userSettings.AppBackdropType;
        }

        public async Task SetBackdropType(BackdropType type)
        {
            _userSettings.AppBackdropType = type;
            await SaveSettingsAsync();
        }

        public async Task AddDirectoryAsync(DirectoryCategory category, string path)
        {
            switch (category)
            {
                case DirectoryCategory.Asset:
                    _userSettings.AssetDirectories.Add(path);
                    break;
                case DirectoryCategory.Image:
                    _userSettings.ImageDirectories.Add(path);
                    break;
                case DirectoryCategory.Project:
                    _userSettings.ProjectDirectories.Add(path);
                    break;
            }
            await SaveSettingsAsync();
        }

        public async Task RemoveDirectoryAsync(DirectoryCategory category, string path)
        {
            switch (category)
            {
                case DirectoryCategory.Asset:
                    _userSettings.AssetDirectories.Remove(path);
                    break;
                case DirectoryCategory.Image:
                    _userSettings.ImageDirectories.Remove(path);
                    break;
                case DirectoryCategory.Project:
                    _userSettings.ProjectDirectories.Remove(path);
                    break;
            }
            await SaveSettingsAsync();
        }

        private UserSettings LoadSettings()
        {
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
                }
                catch (JsonException /* ex */)
                {
                    // ロギングを追加すべきだが、ここでは簡略化
                    return new UserSettings();
                }
            }
            return new UserSettings();
        }

        private async Task SaveSettingsAsync()
        {
            string json = JsonSerializer.Serialize(_userSettings, new JsonSerializerOptions { WriteIndented = true });
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
