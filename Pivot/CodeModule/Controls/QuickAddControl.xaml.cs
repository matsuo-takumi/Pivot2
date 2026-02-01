using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.System;

namespace Pivot.CodeModule.Controls
{
    public sealed partial class QuickAddControl : UserControl
    {
        public event EventHandler<QuickAddEventArgs>? SnippetCreated;

        private bool _isExpanded = false;
        
        // Available tags from existing snippets (set by parent)
        private readonly ObservableCollection<string> _availableTags = new();
        
        // Currently selected tags for this snippet
        private readonly HashSet<string> _selectedTags = new(StringComparer.OrdinalIgnoreCase);

        public QuickAddControl()
        {
            this.InitializeComponent();
            
            // Use AddHandler with handledEventsToo=true to capture keyboard events
            this.AddHandler(KeyDownEvent, new KeyEventHandler(OnKeyDownHandler), true);
            
            ExistingTagsRepeater.ItemsSource = _availableTags;
        }

        /// <summary>
        /// Set available tags (called from CodePage when tags are loaded)
        /// </summary>
        public void SetAvailableTags(IEnumerable<string> tags)
        {
            _availableTags.Clear();
            foreach (var tag in tags.Take(20)) // Limit to 20 tags for UI
            {
                _availableTags.Add(tag);
            }
        }

        #region Expand / Collapse

        private void Expand()
        {
            if (_isExpanded) return;
            _isExpanded = true;

            CollapsedPlaceholder.Visibility = Visibility.Collapsed;
            ExpandedPanel.Visibility = Visibility.Visible;

            // Focus on code editor (Google Keep style: focus on content)
            CodeEditor.Focus(FocusState.Programmatic);
        }

        private void Collapse(bool clearFields = true)
        {
            if (!_isExpanded) return;
            _isExpanded = false;

            ExpandedPanel.Visibility = Visibility.Collapsed;
            CollapsedPlaceholder.Visibility = Visibility.Visible;

            if (clearFields)
            {
                ClearFields();
            }
        }

        private void ClearFields()
        {
            TitleBox.Text = string.Empty;
            CodeEditor.Text = string.Empty;
            NewTagBox.Text = string.Empty;
            NewTagBox.Text = string.Empty;
            _selectedTags.Clear();
            
            // Reset all toggle buttons
            ResetTagToggles();
        }

        private void ResetTagToggles()
        {
            // Walk through ItemsRepeater and reset toggles
            for (int i = 0; i < _availableTags.Count; i++)
            {
                var element = ExistingTagsRepeater.TryGetElement(i);
                if (element is ToggleButton toggle)
                {
                    toggle.IsChecked = false;
                }
            }
        }

        #endregion

        #region Event Handlers

        private void CollapsedPlaceholder_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Direct click handler - no focus issues
            Expand();
        }

        private void UserControl_GotFocus(object sender, RoutedEventArgs e)
        {
            // Only expand if focus came from outside
        }

        private void UserControl_LostFocus(object sender, RoutedEventArgs e)
        {
            // Defer the collapse check to allow focus to settle
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                // Check if still expanded
                if (!_isExpanded) return;
                
                // Check if focus moved outside this control
                var focusedElement = FocusManager.GetFocusedElement(this.XamlRoot);
                if (focusedElement is DependencyObject dep)
                {
                    var current = dep;
                    while (current != null)
                    {
                        if (current == this) return; // Still inside
                        current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
                    }
                }

                // Focus moved outside - collapse without saving
                Collapse(clearFields: true);
            });
        }

        private void OnKeyDownHandler(object sender, KeyRoutedEventArgs e)
        {
            // Ctrl + Enter: Save
            if (e.Key == VirtualKey.Enter)
            {
                var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
                if (ctrlState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
                {
                    e.Handled = true;
                    Save();
                    return;
                }
            }

            // Esc: Cancel
            if (e.Key == VirtualKey.Escape)
            {
                e.Handled = true;
                Collapse(clearFields: true);
                return;
            }
        }

        private void UserControl_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Fallback handler (kept for XAML binding)
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Save();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Collapse(clearFields: true);
        }

        private void NewTagBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                AddNewTag();
            }
        }

        private void AddTagButton_Click(object sender, RoutedEventArgs e)
        {
            AddNewTag();
        }

        private void AddNewTag()
        {
            var newTag = NewTagBox.Text?.Trim().TrimStart('#');
            if (string.IsNullOrEmpty(newTag)) return;

            // Add to selected tags
            _selectedTags.Add(newTag);
            
            // Add to available tags if not already there
            if (!_availableTags.Contains(newTag, StringComparer.OrdinalIgnoreCase))
            {
                _availableTags.Add(newTag);
            }
            
            // Find and check the corresponding toggle button
            var index = _availableTags.IndexOf(newTag);
            if (index >= 0)
            {
                var element = ExistingTagsRepeater.TryGetElement(index);
                if (element is ToggleButton toggle)
                {
                    toggle.IsChecked = true;
                }
            }

            NewTagBox.Text = string.Empty;
        }

        #endregion

        #region Save Logic
        
        public void TrySave()
        {
            if (_isExpanded)
            {
                Save();
            }
        }

        private void Save()
        {
            var code = CodeEditor.Text?.Trim();
            if (string.IsNullOrEmpty(code))
            {
                Collapse(clearFields: true);
                return;
            }

            var title = TitleBox.Text?.Trim() ?? string.Empty;
            
            // Detect language from extension or content
            var language = !string.IsNullOrEmpty(title) 
                ? Pivot.CodeModule.Helpers.CodeFileHelper.GetLanguageFromExtension(title) 
                : DetectLanguage(code);

            // Collect selected tags from toggle buttons
            CollectSelectedTags();

            // Raise event for ViewModel to handle
            SnippetCreated?.Invoke(this, new QuickAddEventArgs
            {
                Title = title,
                Language = language,
                Code = code,
                Tags = _selectedTags.ToArray()
            });

            Collapse(clearFields: true);
        }

        private void CollectSelectedTags()
        {
            for (int i = 0; i < _availableTags.Count; i++)
            {
                var element = ExistingTagsRepeater.TryGetElement(i);
                if (element is ToggleButton toggle && toggle.IsChecked == true)
                {
                    _selectedTags.Add(_availableTags[i]);
                }
            }
        }

        private string DetectLanguage(string code)
        {
            // Simple heuristic language detection
            if (code.Contains("def ") || code.Contains("import ") && code.Contains(":"))
                return "python";
            if (code.Contains("function ") || code.Contains("const ") || code.Contains("=>"))
                return "javascript";
            if (code.Contains("public class ") || code.Contains("namespace "))
                return "csharp";
            if (code.Contains("#include") || code.Contains("std::"))
                return "cpp";
            if (code.Contains("SELECT ") || code.Contains("FROM ") || code.Contains("WHERE "))
                return "sql";
            
            return "text"; // Default
        }

        #endregion
    }

    public class QuickAddEventArgs : EventArgs
    {
        public string Title { get; set; } = string.Empty;
        public string Language { get; set; } = "text";
        public string Code { get; set; } = string.Empty;
        public string[] Tags { get; set; } = Array.Empty<string>();
    }
}
