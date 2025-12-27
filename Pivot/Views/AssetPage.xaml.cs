using Microsoft.UI.Xaml.Controls;
using Pivot.ViewModels;

namespace Pivot.Views
{
    public sealed partial class AssetPage : Page
    {
        public AssetViewModel ViewModel { get; }

        public AssetPage()
        {
            this.InitializeComponent();
            ViewModel = new AssetViewModel();
            this.DataContext = ViewModel;
        }
    }
}
