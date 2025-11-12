using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.Extensions.DependencyInjection;
using Pivot.Services;
using System;
using Pivot.ViewModels;

namespace Pivot.Views
{
    public sealed partial class ColorSettingsPage : Page
    {
        private SettingsService? _settings;

        public ColorSettingsViewModel ViewModel { get; } 

        public ColorSettingsPage()
        {
            this.InitializeComponent();
            _settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
            ViewModel = new ViewModels.ColorSettingsViewModel(_settings);
            this.DataContext = this;
        }
    }
}


