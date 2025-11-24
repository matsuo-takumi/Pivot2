using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Pivot.Services;
using Pivot.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace Pivot.ViewModels
{
    public partial class ColorSettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settings;

        public TextColorSettingsViewModel TextColorSettings { get; }

        public IReadOnlyList<TextColorTemplateOption> TemplateOptions { get; }

        public ColorSettingsViewModel(SettingsService settings, ITextColorResourceManager resourceManager)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            TextColorSettings = new TextColorSettingsViewModel(settings, resourceManager);
            TemplateOptions = new List<TextColorTemplateOption>
            {
                new("Default", TextColorTemplate.Default),
                new("Dominant", TextColorTemplate.Dominant),
                new("Random", TextColorTemplate.Random),
                new("Full Control", TextColorTemplate.FullControl),
            };

            SelectedTemplate = _settings.GetTextColorTemplate();
            DominantColor = TextColorHelper.ParseHexOrDefault(_settings.GetDominantColor(), Colors.CornflowerBlue);
            VariationAmount = _settings.GetDominantVariation();
            GenerateAccentAutomatically = _settings.GetDominantGenerateAccent();
            AccentStrength = _settings.GetDominantAccentStrength();

            RandomSeed = _settings.GetRandomSeed();
            RandomnessLevel = _settings.GetRandomnessLevel();
            RandomSaturationMin = _settings.GetRandomSaturationMin();
            RandomSaturationMax = _settings.GetRandomSaturationMax();
            RandomBrightnessMin = _settings.GetRandomBrightnessMin();
            RandomBrightnessMax = _settings.GetRandomBrightnessMax();
            AllowExtremeRandomColors = _settings.GetRandomAllowExtreme();
        }

        private Color _dominantColor = Colors.CornflowerBlue;
        public Color DominantColor
        {
            get => _dominantColor;
            set
            {
                if (SetProperty(ref _dominantColor, value))
                {
                    _ = _settings.SetDominantColorAsync(TextColorHelper.FormatHex(value));
                }
            }
        }

        private double _variationAmount = 0.25;
        public double VariationAmount
        {
            get => _variationAmount;
            set
            {
                if (SetProperty(ref _variationAmount, value))
                {
                    _ = _settings.SetDominantVariationAsync(value);
                }
            }
        }

        private bool _generateAccentAutomatically = true;
        public bool GenerateAccentAutomatically
        {
            get => _generateAccentAutomatically;
            set
            {
                if (SetProperty(ref _generateAccentAutomatically, value))
                {
                    _ = _settings.SetDominantGenerateAccentAsync(value);
                }
            }
        }

        private double _accentStrength = 0.5;
        public double AccentStrength
        {
            get => _accentStrength;
            set
            {
                if (SetProperty(ref _accentStrength, value))
                {
                    _ = _settings.SetDominantAccentStrengthAsync(value);
                }
            }
        }

        private int _randomSeed = 42;
        public int RandomSeed
        {
            get => _randomSeed;
            set
            {
                if (SetProperty(ref _randomSeed, value))
                {
                    _ = _settings.SetRandomSeedAsync(value);
                }
            }
        }

        private double _randomnessLevel = 0.5;
        public double RandomnessLevel
        {
            get => _randomnessLevel;
            set
            {
                if (SetProperty(ref _randomnessLevel, value))
                {
                    _ = _settings.SetRandomnessLevelAsync(value);
                }
            }
        }

        private double _randomSaturationMin = 0.2;
        public double RandomSaturationMin
        {
            get => _randomSaturationMin;
            set
            {
                if (SetProperty(ref _randomSaturationMin, value))
                {
                    _ = _settings.SetRandomSaturationMinAsync(value);
                }
            }
        }

        private double _randomSaturationMax = 0.8;
        public double RandomSaturationMax
        {
            get => _randomSaturationMax;
            set
            {
                if (SetProperty(ref _randomSaturationMax, value))
                {
                    _ = _settings.SetRandomSaturationMaxAsync(value);
                }
            }
        }

        private double _randomBrightnessMin = 0.2;
        public double RandomBrightnessMin
        {
            get => _randomBrightnessMin;
            set
            {
                if (SetProperty(ref _randomBrightnessMin, value))
                {
                    _ = _settings.SetRandomBrightnessMinAsync(value);
                }
            }
        }

        private double _randomBrightnessMax = 0.8;
        public double RandomBrightnessMax
        {
            get => _randomBrightnessMax;
            set
            {
                if (SetProperty(ref _randomBrightnessMax, value))
                {
                    _ = _settings.SetRandomBrightnessMaxAsync(value);
                }
            }
        }

        private bool _allowExtremeRandomColors;
        public bool AllowExtremeRandomColors
        {
            get => _allowExtremeRandomColors;
            set
            {
                if (SetProperty(ref _allowExtremeRandomColors, value))
                {
                    _ = _settings.SetRandomAllowExtremeAsync(value);
                }
            }
        }

        private TextColorTemplate _selectedTemplate = TextColorTemplate.FullControl;
        public TextColorTemplate SelectedTemplate
        {
            get => _selectedTemplate;
            set
            {
                if (SetProperty(ref _selectedTemplate, value))
                {
                    UpdateTemplateVisibility();
                    _ = _settings.SetTextColorTemplateAsync(value);
                }
            }
        }

        private int _templateIndex;
        public int TemplateIndex
        {
            get => _templateIndex;
            set
            {
                if (SetProperty(ref _templateIndex, value))
                {
                    if (Enum.IsDefined(typeof(TextColorTemplate), value))
                    {
                        SelectedTemplate = (TextColorTemplate)value;
                    }
                }
            }
        }

        public bool IsDefaultTemplate => SelectedTemplate == TextColorTemplate.Default;
        public bool IsDominantTemplate => SelectedTemplate == TextColorTemplate.Dominant;
        public bool IsRandomTemplate => SelectedTemplate == TextColorTemplate.Random;
        public bool IsFullControlTemplate => SelectedTemplate == TextColorTemplate.FullControl;

        public string SelectedTemplateDisplayName =>
            TemplateOptions.FirstOrDefault(opt => opt.Template == SelectedTemplate)?.DisplayName ?? SelectedTemplate.ToString();

        private void UpdateTemplateVisibility()
        {
            if ((int)SelectedTemplate != TemplateIndex)
            {
                _templateIndex = (int)SelectedTemplate;
                OnPropertyChanged(nameof(TemplateIndex));
            }

            OnPropertyChanged(nameof(IsDefaultTemplate));
            OnPropertyChanged(nameof(IsDominantTemplate));
            OnPropertyChanged(nameof(IsRandomTemplate));
            OnPropertyChanged(nameof(IsFullControlTemplate));
            OnPropertyChanged(nameof(SelectedTemplateDisplayName));
        }

    }
}


