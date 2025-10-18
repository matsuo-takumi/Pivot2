using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Pivot.ViewModels;

namespace Pivot.Views
{
    public sealed partial class DirectoryPage : Page
    {
        public DirectoryViewModel ViewModel { get; }

        public DirectoryPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<DirectoryViewModel>();
            this.DataContext = ViewModel;
        }
    }
}


