using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pivot.ViewModels;

namespace Pivot.Views
{
    public sealed partial class WindowPage : Page
    {
        public WindowPage()
        {
            this.InitializeComponent();
            this.DataContext = App.Current.Services.GetRequiredService<WindowViewModel>();
        }
    }
}
