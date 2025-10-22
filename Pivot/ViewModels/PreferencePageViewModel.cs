using CommunityToolkit.Mvvm.ComponentModel;
using Pivot.Views;
using System;

namespace Pivot.ViewModels
{
    public partial class PreferencePageViewModel : ObservableObject
    {
        [ObservableProperty]
        private object? currentContent;

        [ObservableProperty]
        private string selectedMenuTag = "Directory"; // 初期選択をDirectoryに設定

        public PreferencePageViewModel()
        {
            SelectMenuItem("Directory"); // 初期選択をDirectoryに設定
        }

        public void SelectMenuItem(string tag)
        {
            SelectedMenuTag = tag;
            
            // Create appropriate content based on tag
            CurrentContent = tag switch
            {
                "Directory" => new DirectoryPage(),
                "Theme" => new ThemePage(),
                "MenuItem2" => new MenuItem2ContentControl(),
                "MenuItem3" => new MenuItem3ContentControl(),
                "MenuItem4" => new MenuItem4ContentControl(),
                _ => null
            };
        }
    }
}
