using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Pivot.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Pivot.Models;
using System.Linq;
using System.Threading.Tasks;

namespace Pivot.Views
{
    public sealed partial class PreferenceAssetPage : Page
    {
        private TagFilterViewModel? Vm => App.Current.Services.GetService<TagFilterViewModel>();

        public PreferenceAssetPage()
        {
            this.InitializeComponent();
            this.DataContext = Vm;
            _ = Vm?.LoadAvailableTagsAsync();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (Vm != null)
            {
                var td = new TagDefinition { Name = string.Empty, Extensions = new System.Collections.Generic.List<string>(), IsEditing = true, OriginalName = null };
                Vm.AvailableTags.Insert(0, td);
            }
        }

        private void EditItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TagDefinition td)
            {
                td.OriginalName = td.Name;
                td.IsEditing = true;
            }
        }

        private async void DoneItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TagDefinition td && Vm != null)
            {
                var name = td.Name ?? string.Empty;
                var exts = td.ExtensionsCsv ?? string.Empty;
                if (string.IsNullOrWhiteSpace(td.OriginalName))
                {
                    // new tag
                    await Vm.AddTagAsync(name, exts);
                }
                else
                {
                    await Vm.UpdateTagAsync(td.OriginalName, name, exts);
                }
                // reload authoritative list
                await Vm.LoadAvailableTagsAsync();
            }
        }

        private async void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TagDefinition td && Vm != null)
            {
                await Vm.DeleteTagAsync(td.Name);
                await Vm.LoadAvailableTagsAsync();
            }
        }

        private async void ExtensionsTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (sender is TextBox tb && tb.DataContext is TagDefinition td && Vm != null)
                {
                    var name = td.Name ?? string.Empty;
                    var exts = td.ExtensionsCsv ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(td.OriginalName))
                    {
                        await Vm.AddTagAsync(name, exts);
                    }
                    else
                    {
                        await Vm.UpdateTagAsync(td.OriginalName, name, exts);
                    }
                    await Vm.LoadAvailableTagsAsync();
                    e.Handled = true;
                }
            }
        }
    }
}
