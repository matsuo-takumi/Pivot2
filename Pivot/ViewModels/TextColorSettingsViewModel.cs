using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Pivot.Services;
using Pivot.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace Pivot.ViewModels
{
    public class TextColorSettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settings;
        private readonly ITextColorResourceManager _resourceManager;
        private bool _isLoading;

        public ObservableCollection<TextColorSettingViewModel> Entries { get; } = new BatchObservableCollection<TextColorSettingViewModel>();
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public TextColorSettingsViewModel(SettingsService settings, ITextColorResourceManager resourceManager)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
            // Don't create entries synchronously - load them asynchronously after page loads
        }

        public async Task LoadEntriesAsync()
        {
            if (IsLoading || Entries.Count > 0) return;
            IsLoading = true;
            
            try
            {
                // Yield to UI thread first to allow page to render
                await Task.Yield();
                
                // Create all entries off the UI thread (settings lookups are cached and fast)
                var entriesToAdd = new List<TextColorSettingViewModel>(TextColorRoleDefinitions.Roles.Count);
                foreach (var definition in TextColorRoleDefinitions.Roles)
                {
                    var hex = _settings.GetTextColorOverride(definition.SettingKey, definition.DefaultHex);
                    var color = TextColorHelper.ParseHexOrDefault(hex, definition.DefaultColor);
                    var entry = new TextColorSettingViewModel(
                        definition.SettingKey,
                        definition.DisplayName,
                        definition.Description,
                        color,
                        OnColorChanged);
                    entriesToAdd.Add(entry);
                }
                
                // Add all entries at once on UI thread - triggers only one CollectionChanged event
                if (Entries is BatchObservableCollection<TextColorSettingViewModel> batchCollection)
                {
                    batchCollection.AddRange(entriesToAdd);
                }
                else
                {
                    // Fallback: add one by one
                    foreach (var entry in entriesToAdd)
                    {
                        Entries.Add(entry);
                    }
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OnColorChanged(TextColorSettingViewModel entry)
        {
            if (entry == null) return;
            _resourceManager.ApplyColor(entry.SettingKey, entry.SelectedColor);
            _ = PersistSelectionAsync(entry);
        }

        private async Task PersistSelectionAsync(TextColorSettingViewModel entry)
        {
            try
            {
                await _settings.SetTextColorOverrideAsync(entry.SettingKey, entry.HexValue);
            }
            catch
            {
                // Silently ignore persistence errors to avoid disrupting UI responsiveness
                // Errors are logged by SettingsService internally
            }
        }
    }

    public class TextColorSettingViewModel : ObservableObject
    {
        private readonly Action<TextColorSettingViewModel>? _colorChangedCallback;
        private Color _selectedColor;
        private string _hexValue;
        private SolidColorBrush? _previewBrush;

        public TextColorSettingViewModel(string settingKey, string displayName, string description, Color initialColor, Action<TextColorSettingViewModel>? onColorChanged)
        {
            SettingKey = settingKey;
            DisplayName = displayName;
            Description = description;
            _selectedColor = initialColor;
            _hexValue = FormatHex(initialColor);
            // Don't create brush immediately - create it lazily when accessed
            _colorChangedCallback = onColorChanged;
        }

        public string SettingKey { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public Color SelectedColor
        {
            get => _selectedColor;
            set
            {
                if (EqualityComparer<Color>.Default.Equals(_selectedColor, value)) return;
                _selectedColor = value;
                OnPropertyChanged(nameof(SelectedColor));
                var hex = FormatHex(value);
                if (_hexValue != hex)
                {
                    _hexValue = hex;
                    OnPropertyChanged(nameof(HexValue));
                }
                // Update brush if it exists, otherwise it will be created lazily
                if (_previewBrush != null)
                {
                    _previewBrush.Color = value;
                }
                _colorChangedCallback?.Invoke(this);
            }
        }

        public string HexValue
        {
            get => _hexValue;
            set
            {
                var normalized = NormalizeHex(value);
                if (SetProperty(ref _hexValue, normalized))
                {
                    if (TryParseHex(normalized, out var parsed))
                    {
                        SelectedColor = parsed;
                    }
                }
            }
        }

        public SolidColorBrush PreviewBrush
        {
            get
            {
                // Lazy initialization - create brush only when actually accessed (when item becomes visible)
                if (_previewBrush == null)
                {
                    _previewBrush = new SolidColorBrush(_selectedColor);
                }
                return _previewBrush!;
            }
        }

        private static string FormatHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        private static string NormalizeHex(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var trimmed = value.Trim();
            if (!trimmed.StartsWith("#")) trimmed = "#" + trimmed;
            if (trimmed.Length != 7) return trimmed.ToUpperInvariant();
            return trimmed.ToUpperInvariant();
        }

        private static bool TryParseHex(string hex, out Color result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            try
            {
                var value = hex.StartsWith("#") ? hex : "#" + hex;
                if (value.Length != 7) return false;
                result = ColorHelper.FromArgb(255,
                    Convert.ToByte(value.Substring(1, 2), 16),
                    Convert.ToByte(value.Substring(3, 2), 16),
                    Convert.ToByte(value.Substring(5, 2), 16));
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// ObservableCollection that supports batch AddRange to minimize CollectionChanged events
    /// </summary>
    public class BatchObservableCollection<T> : ObservableCollection<T>
    {
        private bool _suppressNotifications;

        public void AddRange(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            _suppressNotifications = true;
            try
            {
                foreach (var item in items)
                {
                    Items.Add(item);
                }
            }
            finally
            {
                _suppressNotifications = false;
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_suppressNotifications)
            {
                base.OnCollectionChanged(e);
            }
        }
    }
}

