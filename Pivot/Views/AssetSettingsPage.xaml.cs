using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pivot.Services;
using Pivot.ViewModels;
using Pivot.Models;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Pivot.Views
{
    public sealed partial class AssetSettingsPage : Page
    {
        public AssetSettingsPage()
        {
            this.InitializeComponent();
            this.Loaded += AssetSettingsPage_Loaded;
        }

        private void AssetSettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize common filter settings view
            var settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
            var logger = App.Current.Services.GetService(typeof(ILogger<FilterSettingsViewModel>)) as ILogger<FilterSettingsViewModel>;
            
            if (settings != null)
            {
                var filterView = this.FindName("FilterSettingsView") as FilterSettingsView;
                if (filterView != null)
                {
                    filterView.ViewModel = new FilterSettingsViewModel(
                        settings,
                        FilterType.Asset,
                        "Asset",
                        GetDefaultAssetFilters,
                        logger);
                }
            }
        }

        private static List<CustomFilter> GetDefaultAssetFilters()
        {
            // デフォルトタグなし - ユーザーが自分で作成
            return new List<CustomFilter>();
        }
    }
}
