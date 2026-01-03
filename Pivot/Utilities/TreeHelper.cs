using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Pivot.Utilities
{
    public static class TreeHelper
    {
        /// <summary>
        /// Checks if any ancestor of the starting object has the specified name.
        /// </summary>
        public static bool IsAncestorNamed(DependencyObject start, string name)
        {
            try
            {
                var current = start;
                while (current != null)
                {
                    if (current is FrameworkElement fe && fe.Name == name) return true;
                    current = VisualTreeHelper.GetParent(current);
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Finds the first descendant of the specified type.
        /// </summary>
        public static T? FindDescendantOfType<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return default;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var found = FindDescendantOfType<T>(child);
                if (found != null) return found;
            }
            return default;
        }

        /// <summary>
        /// Finds the first ancestor whose DataContext is of the specified type.
        /// </summary>
        public static T? FindAncestorWithDataContext<T>(DependencyObject start) where T : class
        {
            var current = start;
            while (current != null)
            {
                if (current is FrameworkElement fe && fe.DataContext is T t) return t;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        /// <summary>
        /// Finds a descendant with the specified Name.
        /// </summary>
        public static DependencyObject? FindDescendantByName(DependencyObject parent, string name)
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe && fe.Name == name) return child;
                
                var found = FindDescendantByName(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
