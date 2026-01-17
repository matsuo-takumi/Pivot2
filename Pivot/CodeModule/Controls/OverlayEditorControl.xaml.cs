using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.Web.WebView2.Core;
using Pivot.CodeModule.ViewModels;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Pivot.CodeModule.Controls
{
    public sealed partial class OverlayEditorControl : UserControl
    {
        public CodeEditorViewModel? ViewModel => DataContext as CodeEditorViewModel;
        
        private bool _isMonacoReady = false;
        private string _pendingContent = string.Empty;
        private string _pendingLanguage = "plaintext";
        private bool _isUpdatingFromMonaco = false;  // Prevent update loop

        public OverlayEditorControl()
        {
            this.InitializeComponent();
            this.DataContextChanged += OnDataContextChanged;
            this.Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await InitializeMonacoAsync();
        }

        private async Task InitializeMonacoAsync()
        {
            try
            {
                await MonacoEditor.EnsureCoreWebView2Async();
                
                // Set up message handler
                MonacoEditor.WebMessageReceived += MonacoEditor_WebMessageReceived;
                
                // Navigate to local Monaco HTML
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var monacoPath = Path.Combine(appDir, "Assets", "Monaco", "editor.html");
                
                if (File.Exists(monacoPath))
                {
                    MonacoEditor.Source = new Uri(monacoPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Monaco init failed: {ex.Message}");
            }
        }

        private void MonacoEditor_WebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                var message = JsonSerializer.Deserialize<MonacoMessage>(args.WebMessageAsJson);
                if (message?.type == "ready")
                {
                    _isMonacoReady = true;
                    // Set pending content if any
                    if (!string.IsNullOrEmpty(_pendingContent))
                    {
                        SetMonacoContent(_pendingContent, _pendingLanguage);
                    }
                }
                else if (message?.type == "contentChanged" && ViewModel != null)
                {
                    _isUpdatingFromMonaco = true;
                    ViewModel.TextContent = message.content ?? string.Empty;
                    _isUpdatingFromMonaco = false;
                }
            }
            catch { }
        }

        private async void SetMonacoContent(string content, string language)
        {
            if (!_isMonacoReady || MonacoEditor.CoreWebView2 == null)
            {
                _pendingContent = content;
                _pendingLanguage = language;
                return;
            }

            try
            {
                var escapedContent = JsonSerializer.Serialize(content);
                await MonacoEditor.ExecuteScriptAsync($"window.monacoApi.setContent({escapedContent})");
                await MonacoEditor.ExecuteScriptAsync($"window.monacoApi.setLanguage('{language}')");
            }
            catch { }
        }

        private string GetLanguageFromExtension(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return "plaintext";
            
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".py" => "python",
                ".js" => "javascript",
                ".ts" => "typescript",
                ".cs" => "csharp",
                ".cpp" or ".cc" or ".cxx" => "cpp",
                ".c" => "c",
                ".h" or ".hpp" => "cpp",
                ".java" => "java",
                ".go" => "go",
                ".rs" => "rust",
                ".rb" => "ruby",
                ".php" => "php",
                ".swift" => "swift",
                ".kt" => "kotlin",
                ".sql" => "sql",
                ".html" or ".htm" => "html",
                ".css" => "css",
                ".scss" => "scss",
                ".json" => "json",
                ".xml" => "xml",
                ".yaml" or ".yml" => "yaml",
                ".md" => "markdown",
                ".sh" or ".bash" => "shell",
                ".ps1" => "powershell",
                ".bat" or ".cmd" => "bat",
                ".vex" => "cpp", // VEX is C-like
                _ => "plaintext"
            };
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
                RefreshBindings();
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.CurrentSnippet) || 
                e.PropertyName == nameof(ViewModel.IsEditing))
            {
                RefreshBindings();
            }
            // Update Monaco when TextContent changes (async file load)
            // but only if not coming from Monaco itself
            else if (e.PropertyName == nameof(ViewModel.TextContent) && !_isUpdatingFromMonaco)
            {
                var language = GetLanguageFromExtension(ViewModel?.CurrentSnippet?.FilePath);
                SetMonacoContent(ViewModel?.TextContent ?? string.Empty, language);
            }
        }

        private void RefreshBindings()
        {
            if (ViewModel == null) return;

            var titleBox = this.FindName("TitleBox") as TextBox;
            
            if (titleBox != null)
                titleBox.Text = ViewModel.CurrentSnippet?.FileName ?? string.Empty;
            
            // Set Monaco content
            var language = GetLanguageFromExtension(ViewModel.CurrentSnippet?.FilePath);
            SetMonacoContent(ViewModel.TextContent ?? string.Empty, language);
            
            ExistingTagsRepeater.ItemsSource = ViewModel.AvailableTags;
            UpdateTagToggles();
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

        private void UpdateTagToggles()
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
            ViewModel?.CloseEditorAsync();
        }

        private void Tag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggle && toggle.Content is string tag && ViewModel != null)
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
            if (e.Key == Windows.System.VirtualKey.Enter && ViewModel != null)
            {
                e.Handled = true;
                var newTagBox = sender as TextBox;
                var tag = newTagBox?.Text?.Trim()?.TrimStart('#');
                if (!string.IsNullOrEmpty(tag))
                {
                    if (!ViewModel.AvailableTags.Contains(tag))
                    {
                        ViewModel.AvailableTags.Add(tag);
                    }
                    if (!ViewModel.Tags.Contains(tag))
                    {
                        ViewModel.Tags.Add(tag);
                    }
                    if (newTagBox != null) newTagBox.Text = string.Empty;
                    UpdateTagToggles();
                }
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                var titleBox = this.FindName("TitleBox") as TextBox;
                
                if (ViewModel.CurrentSnippet != null && titleBox != null)
                {
                    ViewModel.CurrentSnippet.FileName = titleBox.Text;
                }
                
                // TextContent is already synced via WebMessageReceived
                await ViewModel.SaveContentAsync();
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.DeleteCommand?.CanExecute(null) == true)
            {
                await ViewModel.DeleteCommand.ExecuteAsync(null);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.CloseEditorAsync();
        }

        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            string content = ViewModel?.TextContent ?? string.Empty;
            
            // Try to get latest from Monaco
            if (_isMonacoReady && MonacoEditor.CoreWebView2 != null)
            {
                try
                {
                    var result = await MonacoEditor.ExecuteScriptAsync("window.monacoApi.getContent()");
                    if (!string.IsNullOrEmpty(result) && result != "null")
                    {
                        content = JsonSerializer.Deserialize<string>(result) ?? content;
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(content))
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(content);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            }
        }

        private class MonacoMessage
        {
            public string? type { get; set; }
            public string? content { get; set; }
        }
    }
}
