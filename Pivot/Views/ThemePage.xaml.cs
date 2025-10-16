using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pivot.ViewModels;
using System; // Enum.GetValuesを使用するために追加
using System.Linq; // Selectを使用するために追加
using System.Collections.Generic; // IEnumerableを使用するために追加
using Microsoft.UI.Xaml; // ElementThemeを使用するために追加

namespace Pivot.Views
{
    public sealed partial class ThemePage : Page
    {
        public ThemePage()
        {
            this.InitializeComponent();
            this.DataContext = App.Current.Services.GetRequiredService<ThemeViewModel>();
        }
    }
}
