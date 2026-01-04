using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls.Primitives; // ToggleButton
using Pivot.CodeModule.ViewModels;

namespace Pivot.CodeModule.Controls
{
    public sealed partial class OverlayEditorControl : UserControl
    {
        public CodeEditorViewModel ViewModel => (CodeEditorViewModel)this.DataContext;

        public OverlayEditorControl()
        {
            this.InitializeComponent();
            this.Loaded += OverlayEditorControl_Loaded;
            this.Unloaded += OverlayEditorControl_Unloaded;
        }

        private void OverlayEditorControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
                UpdateToggles();
            }
        }

        private void OverlayEditorControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CurrentSnippet")
            {
                UpdateToggles();
            }
        }

        private void ExistingTagsRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            if (args.Element is ToggleButton toggle && ViewModel?.Tags != null)
            {
                var tag = toggle.DataContext as string;
                if (!string.IsNullOrEmpty(tag))
                {
                    toggle.IsChecked = ViewModel.Tags.Contains(tag);
                }
            }
        }

        private void UpdateToggles()
        {
            if (ExistingTagsRepeater == null || ViewModel?.AvailableTags == null || ViewModel.Tags == null) return;

            for (int i = 0; i < ViewModel.AvailableTags.Count; i++)
            {
                var element = ExistingTagsRepeater.TryGetElement(i);
                if (element is ToggleButton toggle)
                {
                    var tag = ViewModel.AvailableTags[i];
                    toggle.IsChecked = ViewModel.Tags.Contains(tag);
                }
            }
        }

        private void Background_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Close on background click
            ViewModel?.CloseEditor();
        }

        private void Tag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggle && toggle.Content is string tag)
            {
                if (toggle.IsChecked == true)
                {
                    if (!ViewModel.Tags.Contains(tag)) ViewModel.Tags.Add(tag);
                }
                else
                {
                    if (ViewModel.Tags.Contains(tag)) ViewModel.Tags.Remove(tag);
                }
            }
        }

        private void NewTagBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                var tag = (sender as TextBox)?.Text?.Trim()?.TrimStart('#');
                if (!string.IsNullOrEmpty(tag))
                {
                    // Add to AvailableTags if not there
                    if (!ViewModel.AvailableTags.Contains(tag))
                    {
                        ViewModel.AvailableTags.Add(tag);
                    }
                     // Add to Selected Tags
                    if (!ViewModel.Tags.Contains(tag))
                    {
                        ViewModel.Tags.Add(tag);
                    }
                    (sender as TextBox).Text = string.Empty;
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.CloseEditor();
        }
    }
}
