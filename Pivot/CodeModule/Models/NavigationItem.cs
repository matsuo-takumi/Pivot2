using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Pivot.CodeModule.Models
{
    /// <summary>
    /// Represents a navigation item in the Code tab's left navigation pane.
    /// Used for data binding instead of building menu items in code-behind.
    /// </summary>
    public partial class NavigationItem : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private Guid _filterId = Guid.Empty;

        [ObservableProperty]
        private bool _isAllSnippets = false;

        [ObservableProperty]
        private string _iconGlyph = "\uE1D3"; // Tag icon by default

        public NavigationItem() { }

        public NavigationItem(string name, Guid filterId, bool isAllSnippets = false, string iconGlyph = "\uE1D3")
        {
            _name = name;
            _filterId = filterId;
            _isAllSnippets = isAllSnippets;
            _iconGlyph = iconGlyph;
        }

        /// <summary>
        /// Creates an "All Snippets" navigation item.
        /// </summary>
        public static NavigationItem CreateAllSnippets()
        {
            return new NavigationItem("All Snippets", Guid.Empty, true, "\uE71D"); // AllApps icon
        }

        /// <summary>
        /// Creates a filter-based navigation item.
        /// </summary>
        public static NavigationItem CreateFilter(string name, Guid filterId)
        {
            return new NavigationItem(name, filterId, false, "\uE1D3"); // Tag icon
        }
    }
}
