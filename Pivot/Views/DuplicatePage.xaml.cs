using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Pivot.ViewModels;

namespace Pivot.Views
{
    public sealed partial class DuplicatePage : Page
    {
        public DuplicateViewModel ViewModel { get; }

        public DuplicatePage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<DuplicateViewModel>();
        }
    }
}
