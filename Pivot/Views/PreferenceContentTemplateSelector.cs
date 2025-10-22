using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pivot.ViewModels;

namespace Pivot.Views
{
    public class PreferenceContentTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? ThemeTemplate { get; set; }
        public DataTemplate? DefaultTemplate { get; set; }

        protected override DataTemplate? SelectTemplateCore(object item)
        {
            if (item is PreferencePageViewModel)
            {
                return ThemeTemplate;
            }

            return DefaultTemplate;
        }

        protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        {
            return SelectTemplateCore(item);
        }
    }
}
