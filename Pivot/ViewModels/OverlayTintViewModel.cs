using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Pivot.Messages;
using Pivot.Services;
using System;
using System.Threading.Tasks;
using Color = Windows.UI.Color;

namespace Pivot.ViewModels
{
    public partial class OverlayTintViewModel : ObservableObject
    {
        private readonly SettingsService _settings;
        private readonly IMessenger _messenger;
        private bool _isInitializing = true;
        private bool _isUpdatingFromColor;
        private bool _isUpdatingFromHsl;

        private Color _tintColor = DefaultTintColor;
        private double _tintOpacity = 0.7;
        private double _tintLuminosityOpacity = 0.0;
        private double _tintTransitionDurationMs = 250;
        private double _hue;
        private double _saturation;
        private double _lightness;

        public OverlayTintViewModel(SettingsService settings, IMessenger messenger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            TintColor = ParseColor(_settings.GetOverlayTintColor());
            TintOpacity = _settings.GetOverlayTintOpacity();
            TintLuminosityOpacity = _settings.GetOverlayTintLuminosityOpacity();
            TintTransitionDurationMs = _settings.GetOverlayTintTransitionDurationMs();
            UpdateBrush();
            UpdateHslFromColor(TintColor);
            _isInitializing = false;
            StatusText = "Applied";
        }

        private static Color DefaultTintColor => Microsoft.UI.ColorHelper.FromArgb(255, 0, 0, 255);

        [ObservableProperty]
        private string _statusText = string.Empty;

        public AcrylicBrush AcrylicBrush { get; } = new AcrylicBrush
        {
            FallbackColor = Microsoft.UI.Colors.Transparent
        };

        [ObservableProperty]
        private SolidColorBrush _textBrush = new SolidColorBrush(Microsoft.UI.Colors.Black);

        public Color TintColor
        {
            get => _tintColor;
            set
            {
                if (SetProperty(ref _tintColor, value))
                {
                    UpdateBrush();
                    if (!_isUpdatingFromHsl)
                    {
                        _isUpdatingFromColor = true;
                        UpdateHslFromColor(value);
                        _isUpdatingFromColor = false;
                    }
                    if (!_isUpdatingFromColor)
                    {
                        TriggerPersist();
                    }
                }
            }
        }

        public double TintOpacity
        {
            get => _tintOpacity;
            set
            {
                if (SetProperty(ref _tintOpacity, Math.Clamp(value, 0.0, 1.0)))
                {
                    UpdateBrush();
                    TriggerPersist();
                }
            }
        }

        public double TintLuminosityOpacity
        {
            get => _tintLuminosityOpacity;
            set
            {
                if (SetProperty(ref _tintLuminosityOpacity, Math.Clamp(value, 0.0, 1.0)))
                {
                    UpdateBrush();
                    TriggerPersist();
                }
            }
        }

        public double TintTransitionDurationMs
        {
            get => _tintTransitionDurationMs;
            set
            {
                if (SetProperty(ref _tintTransitionDurationMs, Math.Max(0, value)))
                {
                    UpdateBrush();
                    TriggerPersist();
                }
            }
        }

        public double Hue
        {
            get => _hue;
            set
            {
                var normalized = NormalizeHue(value);
                if (SetProperty(ref _hue, normalized) && !_isUpdatingFromColor)
                {
                    UpdateColorFromHsl();
                }
            }
        }

        public double Saturation
        {
            get => _saturation;
            set
            {
                var clamped = Clamp01(value);
                if (SetProperty(ref _saturation, clamped) && !_isUpdatingFromColor)
                {
                    UpdateColorFromHsl();
                }
            }
        }

        public double Lightness
        {
            get => _lightness;
            set
            {
                var clamped = Clamp01(value);
                if (SetProperty(ref _lightness, clamped))
                {
                    UpdateTextBrushFromLightness(clamped);
                    if (!_isUpdatingFromColor)
                    {
                        UpdateColorFromHsl();
                    }
                }
            }
        }

        private void UpdateBrush()
        {
            AcrylicBrush.TintColor = TintColor;
            AcrylicBrush.TintOpacity = Math.Clamp(TintOpacity, 0.0, 1.0);
            AcrylicBrush.TintLuminosityOpacity = Math.Clamp(TintLuminosityOpacity, 0.0, 1.0);
            AcrylicBrush.TintTransitionDuration = TimeSpan.FromMilliseconds(Math.Max(0, TintTransitionDurationMs));
        }

        private void UpdateTextBrushFromLightness(double lightness)
        {
            var brushColor = lightness >= 0.5 ? Microsoft.UI.Colors.Black : Microsoft.UI.Colors.White;
            TextBrush = new SolidColorBrush(brushColor);
        }

        private async Task PersistSettingsAsync()
        {
            StatusText = "Saving…";
            try
            {
                var hex = FormatHex(TintColor);
                await _settings.SetOverlayTintColorAsync(hex);
                await _settings.SetOverlayTintOpacityAsync(TintOpacity);
                await _settings.SetOverlayTintLuminosityOpacityAsync(TintLuminosityOpacity);
                await _settings.SetOverlayTintTransitionDurationMsAsync((int)Math.Round(Math.Max(0, TintTransitionDurationMs)));
                try { _messenger.Send(new OverlayColorChangedMessage(hex)); } catch { }
                StatusText = "Applied";
            }
            catch
            {
                StatusText = "Error";
            }
        }

        private void TriggerPersist()
        {
            if (_isInitializing) return;
            _ = PersistSettingsAsync();
        }

        private void UpdateHslFromColor(Color color)
        {
            var (h, s, l) = ColorToHsl(color);
            _isUpdatingFromColor = true;
            Hue = h;
            Saturation = s;
            Lightness = l;
            _isUpdatingFromColor = false;
        }

        private void UpdateColorFromHsl()
        {
            if (_isUpdatingFromColor) return;
            _isUpdatingFromHsl = true;
            TintColor = HslToColor(Hue, Saturation, Lightness);
            _isUpdatingFromHsl = false;
        }

        private static (double H, double S, double L) ColorToHsl(Color color)
        {
            var r = color.R / 255.0;
            var g = color.G / 255.0;
            var b = color.B / 255.0;
            var max = Math.Max(Math.Max(r, g), b);
            var min = Math.Min(Math.Min(r, g), b);
            var l = (max + min) / 2.0;
            double h = 0.0;
            double s = 0.0;
            if (Math.Abs(max - min) > 0.0001)
            {
                var d = max - min;
                s = d / (1.0 - Math.Abs(2.0 * l - 1.0));
                if (max == r)
                {
                    h = 60.0 * ((g - b) / d);
                }
                else if (max == g)
                {
                    h = 60.0 * (((b - r) / d) + 2.0);
                }
                else
                {
                    h = 60.0 * (((r - g) / d) + 4.0);
                }
            }
            h = NormalizeHue(h);
            return (h, Clamp01(s), Clamp01(l));
        }

        private static Color HslToColor(double h, double s, double l)
        {
            var c = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
            var x = c * (1.0 - Math.Abs((h / 60.0 % 2.0) - 1.0));
            var m = l - c / 2.0;
            double r1 = 0, g1 = 0, b1 = 0;
            if (h < 60)
            {
                r1 = c; g1 = x; b1 = 0;
            }
            else if (h < 120)
            {
                r1 = x; g1 = c; b1 = 0;
            }
            else if (h < 180)
            {
                r1 = 0; g1 = c; b1 = x;
            }
            else if (h < 240)
            {
                r1 = 0; g1 = x; b1 = c;
            }
            else if (h < 300)
            {
                r1 = x; g1 = 0; b1 = c;
            }
            else
            {
                r1 = c; g1 = 0; b1 = x;
            }
            byte Convert(double val) => (byte)Math.Clamp((val + m) * 255.0, 0.0, 255.0);
            return ColorHelper.FromArgb(255, Convert(r1), Convert(g1), Convert(b1));
        }

        private static double NormalizeHue(double value)
        {
            var mod = value % 360.0;
            return mod < 0 ? mod + 360.0 : mod;
        }

        private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);

        private static Color ParseColor(string? hex)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hex)) return DefaultTintColor;
                var value = hex.StartsWith("#") ? hex : "#" + hex;
                if (value.Length != 7) return DefaultTintColor;
                return ColorHelper.FromArgb(255,
                    Convert.ToByte(value.Substring(1, 2), 16),
                    Convert.ToByte(value.Substring(3, 2), 16),
                    Convert.ToByte(value.Substring(5, 2), 16));
            }
            catch
            {
                return DefaultTintColor;
            }
        }

        private static string FormatHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}

