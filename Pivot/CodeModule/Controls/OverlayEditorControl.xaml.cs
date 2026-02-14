using Microsoft.UI.Xaml;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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
            
            // Listen to theme changes to persist custom colors
            this.ActualThemeChanged += OnActualThemeChanged;
            
            _syncTimer = new DispatcherTimer();
            _syncTimer.Interval = TimeSpan.FromMilliseconds(500); // Slower interval for auto-save checks if needed
            _syncTimer.Tick += SyncTimer_Tick;
            
            this.Loaded += (s, e) => {
                 _syncTimer.Start();
                 // Ensure colors are applied on load
                 UpdateEditorColors();
            };
            this.Unloaded += (s, e) => {
                 _syncTimer.Stop();
            };
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            // WinUIEditor might reset styles on theme change, so we re-apply ours
            UpdateEditorColors();
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
                UpdateEditorColors();
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CodeEditorViewModel.TextContent))
            {
                UpdateEditorText();
            }
            else if (e.PropertyName == nameof(CodeEditorViewModel.EditorTextBrush) || 
                     e.PropertyName == nameof(CodeEditorViewModel.EditorBackgroundBrush) ||
                     e.PropertyName == nameof(CodeEditorViewModel.LineNumberBrush))
            {
                UpdateEditorColors();
            }
        }

        private void UpdateEditorColors()
        {
            if (ViewModel == null || CodeEditor?.Editor == null) return;

            try
            {
                var textBrush = ViewModel.EditorTextBrush as Microsoft.UI.Xaml.Media.SolidColorBrush;
                var bgBrush = ViewModel.EditorBackgroundBrush as Microsoft.UI.Xaml.Media.SolidColorBrush;

                // Fallback to theme resources if null (Default/Theme mode)
                if (textBrush == null)
                {
                    if (Application.Current.Resources.TryGetValue("SystemControlPageTextBaseHighBrush", out var res) && res is SolidColorBrush themeBrush)
                    {
                        textBrush = themeBrush;
                    }
                    else
                    {
                        textBrush = new SolidColorBrush(Colors.Black); // Safety fallback
                    }
                }

                if (bgBrush == null)
                {
                    // Use a standard background brush key or just transparent/editor default
                    if (Application.Current.Resources.TryGetValue("SystemControlPageBackgroundChromeLowBrush", out var res) && res is SolidColorBrush themeBrush)
                    {
                        bgBrush = themeBrush;
                    }
                    else
                    {
                         bgBrush = new SolidColorBrush(Colors.White); // Safety fallback
                    }
                }

                if (textBrush != null && bgBrush != null)
                {
                    var textColor = textBrush.Color;
                    var bgColor = bgBrush.Color;
                    
                    // WinUIEditor uses Win32 COLORREF format (0x00BBGGRR)
                    int textColorRef = (int)((textColor.B << 16) | (textColor.G << 8) | textColor.R);
                    int bgColorRef = (int)((bgColor.B << 16) | (bgColor.G << 8) | bgColor.R);
                    
                    // Set default style colors
                    // STYLE_DEFAULT = 32
                    CodeEditor.Editor.StyleSetFore(32, textColorRef);
                    CodeEditor.Editor.StyleSetBack(32, bgColorRef);
                    CodeEditor.Editor.StyleClearAll(); // Apply to all styles

                    // Set line number style colors (Style 33)
                    // The user wants the line number background to match the editor background
                    if (ViewModel.LineNumberBrush is SolidColorBrush lineBrush)
                    {
                        var lineColor = lineBrush.Color;
                        int lineColorRef = (int)((lineColor.B << 16) | (lineColor.G << 8) | lineColor.R);
                        
                        CodeEditor.Editor.StyleSetFore(33, lineColorRef);
                    }
                    else
                    {
                        // Fallback Line Number Color (Gray-ish or Theme Medium)
                        if (Application.Current.Resources.TryGetValue("SystemControlForegroundBaseMediumBrush", out var res) && res is SolidColorBrush themeLineBrush)
                        {
                            var lineColor = themeLineBrush.Color;
                            int lineColorRef = (int)((lineColor.B << 16) | (lineColor.G << 8) | lineColor.R);
                            CodeEditor.Editor.StyleSetFore(33, lineColorRef);
                        }
                    }
                    
                    // Always set gutter back to match editor back
                    CodeEditor.Editor.StyleSetBack(33, bgColorRef); 
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateEditorColors] Failed: {ex.Message}");
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
                    ViewModel.CurrentSnippet = ViewModel.CurrentSnippet with { FileName = titleBox.Text };
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
