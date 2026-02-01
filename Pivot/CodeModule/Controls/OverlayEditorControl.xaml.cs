using Microsoft.UI.Xaml;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Pivot.CodeModule.ViewModels;
using Pivot.CodeModule.Helpers;
using System;
using System.Threading.Tasks;

namespace Pivot.CodeModule.Controls
{
    public sealed partial class OverlayEditorControl : UserControl
    {
        public CodeEditorViewModel? ViewModel => DataContext as CodeEditorViewModel;

        private DispatcherTimer _syncTimer;


        public OverlayEditorControl()
        {
            this.InitializeComponent();
            this.DataContextChanged += OnDataContextChanged;
            
            _syncTimer = new DispatcherTimer();
            _syncTimer.Interval = TimeSpan.FromMilliseconds(500); // Slower interval for auto-save checks if needed
            _syncTimer.Tick += SyncTimer_Tick;
            
            this.Loaded += (s, e) => {
                 _syncTimer.Start();
            };
            this.Unloaded += (s, e) => {
                 _syncTimer.Stop();
            };
        }



        private bool _isSyncing = false;


        private void SyncTimer_Tick(object? sender, object e)
        {
            try
            {
                if (_isSyncing) return;

                // Simple check for content update loop if needed
                // Currently only used for ViewModel sync
                if (ViewModel != null)
                {
                     var mainLen = CodeEditor.Editor.Length;
                     if (mainLen > 0)
                     {
                         var mainText = CodeEditor.Editor.GetText(mainLen + 1);
                         if (mainText != ViewModel.TextContent)
                         {
                             try
                             {
                                 _isSyncing = true;
                                 ViewModel.TextContent = mainText; // This might trigger PropertyChanged -> UpdateEditorText
                             }
                             finally
                             {
                                 _isSyncing = false;
                             }
                         }
                     }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SyncTimer] Error: {ex.Message}");
            }
        }




        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
                
                var titleBox = this.FindName("TitleBox") as TextBox;
                if (titleBox != null)
                {
                    titleBox.Text = ViewModel.CurrentSnippet?.FileName ?? string.Empty;
                }

                UpdateEditorText();
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CodeEditorViewModel.TextContent))
            {
                UpdateEditorText();
            }
        }

        private void UpdateEditorText()
        {
            if (_isSyncing || ViewModel == null)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateEditorText] Skipped - isSyncing:{_isSyncing}, ViewModel null:{ViewModel == null}");
                return;
            }
            
            try 
            {
                _isSyncing = true;
                var text = ViewModel.TextContent ?? string.Empty;
                System.Diagnostics.Debug.WriteLine($"[UpdateEditorText] Syncing {text.Length} chars to editor");
                
                // Update editor
                CodeEditor.Editor.SetText(text);
                
                System.Diagnostics.Debug.WriteLine($"[UpdateEditorText] Sync complete");
            }
            finally
            {
                _isSyncing = false;
            }
        }



        private async void Background_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            await CloseAndSaveAsync();
        }

        private async Task CloseAndSaveAsync()
        {
            if (ViewModel != null)
            {
                var titleBox = this.FindName("TitleBox") as TextBox;
                
                if (ViewModel.CurrentSnippet != null && titleBox != null)
                {
                    ViewModel.CurrentSnippet.FileName = titleBox.Text;
                }
                
                // Get current text from editor and sync to ViewModel
                try
                {
                    var length = CodeEditor.Editor.Length;
                    var editorText = CodeEditor.Editor.GetText(length + 1); // +1 for null terminator
                    ViewModel.TextContent = editorText;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OverlayEditor] Error getting text: {ex.Message}");
                }
                
                // Ensure tags are synced before saving
                if (ViewModel.CurrentSnippet != null)
                {
                    ViewModel.CurrentSnippet.UserTagsJson = System.Text.Json.JsonSerializer.Serialize(ViewModel.Tags);
                    System.Diagnostics.Debug.WriteLine($"[OverlayEditor] Syncing {ViewModel.Tags.Count} tags before save: {string.Join(", ", ViewModel.Tags)}");
                }
                
                await ViewModel.SaveContentAsync();
                await ViewModel.CloseEditorAsync();
            }
        }

        internal void RemoveTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is string tag && ViewModel != null)
            {
                ViewModel.Tags.Remove(tag);
            }
        }

        internal void TagInput_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && ViewModel != null)
            {
                if (string.IsNullOrWhiteSpace(sender.Text))
                {
                    sender.ItemsSource = null;
                }
                else
                {
                    var query = sender.Text.Trim();
                    sender.ItemsSource = ViewModel.AvailableTags
                        .Where(t => t.Contains(query, StringComparison.OrdinalIgnoreCase))
                        .Where(t => !ViewModel.Tags.Contains(t))
                        .Take(10)
                        .ToList();
                }
            }
        }

        internal void TagInput_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion != null)
            {
                AddTag(args.ChosenSuggestion as string);
            }
            else if (!string.IsNullOrWhiteSpace(args.QueryText))
            {
                AddTag(args.QueryText);
            }
            sender.Text = string.Empty;
            sender.ItemsSource = null;
        }

        internal void TagInput_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            // Optional: Update text to match suggestion before submission
        }

        private void AddTag(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || ViewModel == null) return;
            tag = tag.Trim().TrimStart('#');
            
            if (!string.IsNullOrEmpty(tag) && !ViewModel.Tags.Contains(tag))
            {
                ViewModel.Tags.Add(tag);
                
                // Add to available tags if new
                if (!ViewModel.AvailableTags.Contains(tag))
                {
                    ViewModel.AvailableTags.Add(tag);
                }
            }
        }



        private void AvailableTag_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is string tag)
            {
                AddTag(tag);
                AvailableTagsFlyout?.Hide();
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.DeleteCommand?.CanExecute(null) == true)
            {
                await ViewModel.DeleteCommand.ExecuteAsync(null);
            }
        }



        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            string content = ViewModel?.TextContent ?? string.Empty;

            if (!string.IsNullOrEmpty(content))
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(content);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            }
        }
    }
}
