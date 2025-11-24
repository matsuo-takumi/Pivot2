using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pivot.Services;
using System.Linq;
using System;
using Pivot.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Pivot.Views
{
    public sealed partial class ImageSettingsPage : Page
    {
        private readonly SettingsService? _settings;

        public ImageSettingsPage()
        {
            this.InitializeComponent();
            _settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
            this.Loaded += ImageSettingsPage_Loaded;
        }

        private void ImageSettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize common filter settings view
            var logger = App.Current.Services.GetService(typeof(Microsoft.Extensions.Logging.ILogger<ViewModels.FilterSettingsViewModel>)) as Microsoft.Extensions.Logging.ILogger<ViewModels.FilterSettingsViewModel>;
            
            if (_settings != null)
            {
                var filterView = this.FindName("FilterSettingsView") as FilterSettingsView;
                if (filterView != null)
                {
                    filterView.ViewModel = new ViewModels.FilterSettingsViewModel(
                        _settings,
                        Models.FilterType.Image,
                        "Image",
                        GetDefaultImageFilters,
                        logger);
                }
            }
        }

        private static List<CustomFilter> GetDefaultImageFilters()
        {
            return new List<CustomFilter>
            {
                new CustomFilter 
                { 
                    Name = "Images", 
                    AllowedExtensions = new List<string>{ ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tga", ".tif", ".tiff" }, 
                    IsBuiltIn = true 
                }
            };
        }
    }
}

