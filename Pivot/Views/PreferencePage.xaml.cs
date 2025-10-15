using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pivot.ViewModels;

namespace Pivot.Views
{
    public sealed partial class PreferencePage : Page
    {
        public PreferencePage()
        {
            this.InitializeComponent();
            this.DataContext = App.Current.Services.GetRequiredService<DirectoryViewModel>();
        }
    }
}
