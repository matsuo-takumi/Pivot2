using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Pivot.CodeModule.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.System;

namespace Pivot.CodeModule.Controls
{
    public sealed partial class QuickAddControl : UserControl
    {
        // ViewModel property for data binding
        public QuickAddViewModel? ViewModel => DataContext as QuickAddViewModel;

        private bool _isExpanded = false;

        public QuickAddControl()
        {
            this.InitializeComponent();
            
            // Use AddHandler with handledEventsToo=true to capture keyboard events
            this.AddHandler(KeyDownEvent, new KeyEventHandler(OnKeyDownHandler), true);
            
            // Subscribe to ViewModel events when DataContext changes
            this.DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (ViewModel != null)
            {
                // Subscribe to ViewModel's IsExpanded property changes
                ViewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(QuickAddViewModel.IsExpanded))
                    {
                        UpdateExpandedState();
                    }
                };
                
                // Bind ItemsSource for tags
                ExistingTagsRepeater.ItemsSource = ViewModel.AvailableTags;
            }
        }

        private void UpdateExpandedState()
        {
            if (ViewModel == null) return;
            
            _isExpanded = ViewModel.IsExpanded;
            ExpandedPanel.Visibility = _isExpanded ? Visibility.Visible : Visibility.Collapsed;
            
            if (_isExpanded)
            {
                // Focus on code editor when expanded
                DispatcherQueue.TryEnqueue(() =>
                {
                    CodeEditor.Focus(FocusState.Programmatic);
                });
            }
            else
            {
                // Reset tag toggles when collapsed
                ResetTagToggles();
            }
        }

        private void ResetTagToggles()
        {
            // Walk through ItemsRepeater and reset toggles
            if (ViewModel == null) return;
            
            for (int i = 0; i < ViewModel.AvailableTags.Count; i++)
            {
                var element = ExistingTagsRepeater.TryGetElement(i);
                if (element is ToggleButton toggle)
                {
                    toggle.IsChecked = false;
                }
            }
        }

        #region Event Handlers

        private void CollapsedPlaceholder_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            ViewModel?.ExpandCommand.Execute(null);
        }

        private void OnKeyDownHandler(object sender, KeyRoutedEventArgs e)
        {
            // Ctrl+Enter to save
            if (e.Key == VirtualKey.Enter && 
                (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)))
            {
                e.Handled = true;
                ViewModel?.CreateSnippetCommand.Execute(null);
            }
            // Escape to collapse
            else if (e.Key == VirtualKey.Escape && _isExpanded)
            {
                e.Handled = true;
                ViewModel?.CollapseCommand.Execute(null);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.CreateSnippetCommand.Execute(null);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.CollapseCommand.Execute(null);
        }

        private void AddTagButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.AddTagCommand.Execute(null);
        }

        private void NewTagBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                ViewModel?.AddTagCommand.Execute(null);
            }
        }

        private void TagToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggle && toggle.Content is string tag)
            {
                ViewModel?.ToggleTag(tag, true);
            }
        }

        private void TagToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggle && toggle.Content is string tag)
            {
                ViewModel?.ToggleTag(tag, false);
            }
        }

        #endregion

        /// <summary>
        /// Public method to set available tags from external source (e.g., CodePage).
        /// This is a bridge method for backward compatibility.
        /// </summary>
        public void SetAvailableTags(IEnumerable<string> tags)
        {
            ViewModel?.SetAvailableTags(tags);
        }

        /// <summary>
        /// Public method to trigger save from external keyboard shortcut (Ctrl+S).
        /// </summary>
        public void TrySave()
        {
            ViewModel?.TrySave();
        }
    }
}
