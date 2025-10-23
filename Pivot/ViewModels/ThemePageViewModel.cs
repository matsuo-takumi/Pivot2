using CommunityToolkit.Mvvm.ComponentModel;

namespace Pivot.ViewModels
{
    /// <summary>
    /// Container view model that wraps both ThemeViewModel and DirectoryViewModel
    /// for the ThemePage UI.
    /// </summary>
    public class ThemePageViewModel
    {
        public ThemeViewModel ThemeViewModel { get; }
        public DirectoryViewModel DirectoryViewModel { get; }

        public ThemePageViewModel(ThemeViewModel themeViewModel, DirectoryViewModel directoryViewModel)
        {
            ThemeViewModel = themeViewModel;
            DirectoryViewModel = directoryViewModel;
        }
    }
}
