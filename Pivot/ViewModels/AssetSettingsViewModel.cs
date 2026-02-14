using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Engine.Models;
using Pivot.Models;
using Pivot.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace Pivot.ViewModels
{
    public partial class AssetSettingsViewModel : ObservableObject
    {
        private readonly ViewportSettingsService _viewportSettings;
        private readonly MaterialSettingsService _materialSettings;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private CameraGesturePreset _selectedGesturePreset;

        [ObservableProperty]
        private string _rotateGestureText = "Alt + LMB";

        [ObservableProperty]
        private string _panGestureText = "Alt + MMB";

        [ObservableProperty]
        private string _zoomGestureText = "Alt + RMB";

        [ObservableProperty]
        private ViewportBackgroundMode _backgroundMode;

        [ObservableProperty]
        private Color _backgroundColor;

        [ObservableProperty]
        private Color _wireframeColor;

        [ObservableProperty]
        private bool _showAxisGizmo;

        [ObservableProperty]
        private bool _backfaceCulling;

        [ObservableProperty]
        private bool _showPolygonCount;

        [ObservableProperty]
        private bool _showVertexCount;

        [ObservableProperty]
        private bool _showUVSetCount;

        [ObservableProperty]
        private bool _showMaterialCount;

        [ObservableProperty]
        private bool _showBoundingBox;

        [ObservableProperty]
        private bool _showFPS;

        [ObservableProperty]
        private bool _showResolution;

        [ObservableProperty]
        private bool _showViewportSize;

        [ObservableProperty]
        private bool _showCameraInfo;



        [ObservableProperty]
        private ObservableCollection<MaterialPreset> _materialPresets = new();

        [ObservableProperty]
        private ObservableCollection<LightingPreset> _lightingPresets = new();

        [ObservableProperty]
        private MaterialPreset? _selectedMaterialPreset;

        [ObservableProperty]
        private LightingPreset? _selectedLightingPreset;

        // Computed properties for XAML bindings
        public string RotateGesture => RotateGestureText;
        public string PanGesture => PanGestureText;
        public string ZoomGesture => ZoomGestureText;

        public int SelectedGestureIndex
        {
            get => (int)SelectedGesturePreset;
            set
            {
                if (value >= 0 && value <= 2)
                {
                    SelectedGesturePreset = (CameraGesturePreset)value;
                }
            }
        }

        public int SelectedBackgroundModeIndex
        {
            get => (int)BackgroundMode;
            set
            {
                if (value >= 0 && value <= 1)
                {
                    BackgroundMode = (ViewportBackgroundMode)value;
                }
            }
        }

        public Microsoft.UI.Xaml.Visibility BackgroundColorPanelVisibility =>
            BackgroundMode == ViewportBackgroundMode.Custom
                ? Microsoft.UI.Xaml.Visibility.Visible
                : Microsoft.UI.Xaml.Visibility.Collapsed;

        public AssetSettingsViewModel(
            ViewportSettingsService viewportSettings,
            MaterialSettingsService materialSettings,
            IMessenger messenger)
        {
            _viewportSettings = viewportSettings;
            _materialSettings = materialSettings;
            _messenger = messenger;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            // Load viewport settings
            LoadViewportSettings();

            // Load presets
            await LoadPresetsAsync();
        }

        private void LoadViewportSettings()
        {
            SelectedGesturePreset = _viewportSettings.GetCameraGesture();
            UpdateGestureDisplay(SelectedGesturePreset);

            BackgroundMode = _viewportSettings.GetBackgroundMode();
            BackgroundColor = HexToColor(_viewportSettings.GetBackgroundColor());
            WireframeColor = HexToColor(_viewportSettings.GetWireframeColor());

            ShowAxisGizmo = _viewportSettings.GetShowAxisGizmo();
            BackfaceCulling = _viewportSettings.GetBackfaceCulling();

            ShowPolygonCount = _viewportSettings.GetShowPolygonCount();
            ShowVertexCount = _viewportSettings.GetShowVertexCount();
            ShowUVSetCount = _viewportSettings.GetShowUVSetCount();
            ShowMaterialCount = _viewportSettings.GetShowMaterialCount();
            ShowBoundingBox = _viewportSettings.GetShowBoundingBox();

            ShowFPS = _viewportSettings.GetShowFPS();
            ShowResolution = _viewportSettings.GetShowResolution();
            ShowViewportSize = _viewportSettings.GetShowViewportSize();
            ShowCameraInfo = _viewportSettings.GetShowCameraInfo();


        }

        private async Task LoadPresetsAsync()
        {
            var materialPresets = await _materialSettings.GetMaterialPresetsAsync();
            MaterialPresets.Clear();
            foreach (var preset in materialPresets)
            {
                MaterialPresets.Add(preset);
            }

            var lightingPresets = await _materialSettings.GetLightingPresetsAsync();
            LightingPresets.Clear();
            foreach (var preset in lightingPresets)
            {
                LightingPresets.Add(preset);
            }
        }

        partial void OnSelectedGesturePresetChanged(CameraGesturePreset value)
        {
            UpdateGestureDisplay(value);
            _ = _viewportSettings.SetCameraGestureAsync(value);
            OnPropertyChanged(nameof(SelectedGestureIndex));
        }

        private void UpdateGestureDisplay(CameraGesturePreset preset)
        {
            switch (preset)
            {
                case CameraGesturePreset.Maya:
                    RotateGestureText = "Alt + LMB";
                    PanGestureText = "Alt + MMB";
                    ZoomGestureText = "Alt + RMB";
                    break;

                case CameraGesturePreset.Houdini:
                    RotateGestureText = "LMB";
                    PanGestureText = "MMB";
                    ZoomGestureText = "RMB";
                    break;

                case CameraGesturePreset.Blender:
                    RotateGestureText = "MMB";
                    PanGestureText = "Shift + MMB";
                    ZoomGestureText = "Ctrl + MMB";
                    break;
            }
            OnPropertyChanged(nameof(RotateGesture));
            OnPropertyChanged(nameof(PanGesture));
            OnPropertyChanged(nameof(ZoomGesture));
        }

        partial void OnBackgroundModeChanged(ViewportBackgroundMode value)
        {
            _ = _viewportSettings.SetBackgroundModeAsync(value);
            OnPropertyChanged(nameof(SelectedBackgroundModeIndex));
            OnPropertyChanged(nameof(BackgroundColorPanelVisibility));
        }

        partial void OnBackgroundColorChanged(Color value)
        {
            var hex = ColorToHex(value);
            _ = _viewportSettings.SetBackgroundColorAsync(hex);
        }

        partial void OnWireframeColorChanged(Color value)
        {
            var hex = ColorToHex(value);
            _ = _viewportSettings.SetWireframeColorAsync(hex);
        }

        partial void OnShowAxisGizmoChanged(bool value)
        {
            _ = _viewportSettings.SetShowAxisGizmoAsync(value);
        }

        partial void OnBackfaceCullingChanged(bool value)
        {
            _ = _viewportSettings.SetBackfaceCullingAsync(value);
        }

        partial void OnShowPolygonCountChanged(bool value)
        {
            _ = _viewportSettings.SetShowPolygonCountAsync(value);
        }

        partial void OnShowVertexCountChanged(bool value)
        {
            _ = _viewportSettings.SetShowVertexCountAsync(value);
        }

        partial void OnShowUVSetCountChanged(bool value)
        {
            _ = _viewportSettings.SetShowUVSetCountAsync(value);
        }

        partial void OnShowMaterialCountChanged(bool value)
        {
            _ = _viewportSettings.SetShowMaterialCountAsync(value);
        }

        partial void OnShowBoundingBoxChanged(bool value)
        {
            _ = _viewportSettings.SetShowBoundingBoxAsync(value);
        }

        partial void OnShowFPSChanged(bool value)
        {
            _ = _viewportSettings.SetShowFPSAsync(value);
        }

        partial void OnShowResolutionChanged(bool value)
        {
            _ = _viewportSettings.SetShowResolutionAsync(value);
        }

        partial void OnShowViewportSizeChanged(bool value)
        {
            _ = _viewportSettings.SetShowViewportSizeAsync(value);
        }

        partial void OnShowCameraInfoChanged(bool value)
        {
            _ = _viewportSettings.SetShowCameraInfoAsync(value);
        }



        [RelayCommand]
        private async Task AddMaterialPresetAsync(string? presetName)
        {
            if (string.IsNullOrWhiteSpace(presetName)) return;

            var preset = new MaterialPreset
            {
                Name = presetName,
                IsCustom = true,
                Albedo = Color.FromArgb(255, 200, 200, 200),
                Metallic = 0.0f,
                Roughness = 0.5f
            };

            MaterialPresets.Add(preset);
            await _materialSettings.SetMaterialPresetsAsync(MaterialPresets.ToList());
        }

        [RelayCommand]
        private async Task DeleteMaterialPresetAsync(MaterialPreset? preset)
        {
            if (preset == null) return;

            MaterialPresets.Remove(preset);
            await _materialSettings.SetMaterialPresetsAsync(MaterialPresets.ToList());
        }

        [RelayCommand]
        private async Task AddLightingPresetAsync(string? presetName)
        {
            if (string.IsNullOrWhiteSpace(presetName)) return;

            var preset = LightingPreset.GetDefaultPresets().First().Clone();
            preset.Name = presetName;
            preset.IsCustom = true;

            LightingPresets.Add(preset);
            await _materialSettings.SaveLightingPresetsAsync(LightingPresets.ToList());
        }

        [RelayCommand]
        private async Task DeleteLightingPresetAsync(LightingPreset? preset)
        {
            if (preset == null) return;

            LightingPresets.Remove(preset);
            await _materialSettings.SaveLightingPresetsAsync(LightingPresets.ToList());
        }

        private static Color HexToColor(string hex)
        {
            try
            {
                hex = hex.TrimStart('#');
                if (hex.Length == 6)
                {
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    return Color.FromArgb(255, r, g, b);
                }
            }
            catch { }
            return Color.FromArgb(255, 51, 153, 204);
        }

        private static string ColorToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
    }
}
