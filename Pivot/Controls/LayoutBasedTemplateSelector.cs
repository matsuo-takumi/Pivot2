using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pivot.Engine.Models;

namespace Pivot.Controls
{
    /// <summary>
    /// DataTemplateSelector that switches between Card and List templates based on layout mode.
    /// </summary>
    public class LayoutBasedTemplateSelector : DataTemplateSelector
    {
        /// <summary>
        /// Template for Masonry and Grid layouts (card-style with large image).
        /// </summary>
        public DataTemplate? CardTemplate { get; set; }

        /// <summary>
        /// Template for List layout (row-style with small thumbnail and details).
        /// </summary>
        public DataTemplate? ListTemplate { get; set; }

        /// <summary>
        /// Current layout mode. Set this from the ViewModel.
        /// </summary>
        public LayoutType CurrentLayout { get; set; } = LayoutType.Masonry;

        protected override DataTemplate? SelectTemplateCore(object item)
        {
            return CurrentLayout == LayoutType.List ? ListTemplate : CardTemplate;
        }

        protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        {
            return SelectTemplateCore(item);
        }
    }
}
