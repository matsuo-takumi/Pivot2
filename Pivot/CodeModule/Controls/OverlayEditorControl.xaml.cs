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

        public OverlayEditorControl()
        {
            this.InitializeComponent();
            this.DataContextChanged += OnDataContextChanged;
            
            // Lazy load Monaco when editor becomes visible
            this.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, OnVisibilityChanged);
        }

        private async void OnVisibilityChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (this.Visibility == Visibility.Visible)
            {
                var monaco = this.FindName("MonacoEditor") as MonacoEditorControl;
                if (monaco != null)
                {
                    await monaco.EnsureInitializedAsync();
                }
            }
        }


        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            // Bindings are now handled automatically via x:Bind
            if (ViewModel != null)
            {
                var titleBox = this.FindName("TitleBox") as TextBox;
                if (titleBox != null)
                {
                    titleBox.Text = ViewModel.CurrentSnippet?.FileName ?? string.Empty;
                }
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
                
                // Ensure tags are synced before saving
                if (ViewModel.CurrentSnippet != null)
                {
                    ViewModel.CurrentSnippet.UserTagsJson = System.Text.Json.JsonSerializer.Serialize(ViewModel.Tags);
                    System.Diagnostics.Debug.WriteLine($"[OverlayEditor] Syncing {ViewModel.Tags.Count} tags before save: {string.Join(", ", ViewModel.Tags)}");
                }
                
                // TextContent is already synced via two-way binding
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



        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            string content = await MonacoEditor.GetContentAsync();

            if (!string.IsNullOrEmpty(content))
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(content);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            }
        }
    }
}
