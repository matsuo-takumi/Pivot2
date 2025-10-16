using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection; // GetRequiredServiceを使用するために追加
using Pivot.ViewModels; // DirectoryViewModelを使用するために追加

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Pivot.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ProjectPage : Page
    {
        public ProjectPage()
        {
            this.InitializeComponent();
            this.DataContext = App.Current.Services.GetRequiredService<DirectoryViewModel>();
        }
    }
}
