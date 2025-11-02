using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pivot.CodeModule.ViewModels;
using System;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Xaml;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

// WebView2 core types are used if available at runtime
using Microsoft.Web.WebView2.Core;

namespace Pivot.CodeModule.Views
{
    public sealed partial class CodePage : Page
    {
        public CodeViewModel? ViewModel => DataContext as CodeViewModel;

        public CodePage()
        {
            this.InitializeComponent();
            // Resolve via DI if available
            try
            {
                DataContext = App.Current.Services.GetRequiredService<CodeViewModel>();
            }
            catch (Exception)
            {
                // fallback: create a local viewmodel instance so UI still works in environments
                // where DI or EF Core registration failed.
                try
                {
                    DataContext = new CodeViewModel();
                }
                catch { }
            }

            // subscribe to selection changes to push content to editor
            if (DataContext is INotifyPropertyChanged pc)
            {
                pc.PropertyChanged += OnViewModelPropertyChanged;
            }

            // ensure placeholder visibility reflects whether snippets exist
            try
            {
                if (ViewModel?.Snippets != null)
                {
                    var root = this.Content as FrameworkElement;
                    var placeholder = root?.FindName("PlaceholderBorder") as Border;
                    if (placeholder != null)
                    {
                        placeholder.Visibility = ViewModel.Snippets.Any() ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
                    }

                    ViewModel.Snippets.CollectionChanged += (_, __) =>
                    {
                        try
                        {
                            var p2 = (this.Content as FrameworkElement)?.FindName("PlaceholderBorder") as Border;
                            if (p2 != null)
                            {
                                p2.Visibility = ViewModel.Snippets.Any() ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
                            }
                        }
                        catch { }
                    };
                }
            }
            catch { }
        }

        private bool _webMessageHooked = false;

        private void CoreWebView2_WebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                var json = args.WebMessageAsJson;
                if (string.IsNullOrEmpty(json)) return;
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("action", out var actionEl)) return;
                var action = actionEl.GetString();

                if (action == "contentChanged")
                {
                    var content = doc.RootElement.GetProperty("payload").GetProperty("content").GetString();
                    App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                    {
                        if (ViewModel != null && ViewModel.SelectedSnippet != null)
                        {
                            ViewModel.SelectedSnippet.Content = content ?? string.Empty;
                            ViewModel.IsDirty = true;
                        }
                    });
                }
                else if (action == "save")
                {
                    App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(async () =>
                    {
                        if (ViewModel != null && ViewModel.SaveSnippetCommand != null)
                        {
                            if (ViewModel.SaveSnippetCommand is IAsyncRelayCommand asyncCmd)
                            {
                                await asyncCmd.ExecuteAsync(null);
                            }
                            else if (ViewModel.SaveSnippetCommand.CanExecute(null))
                            {
                                ViewModel.SaveSnippetCommand.Execute(null);
                            }
                        }
                    });
                }
            }
            catch { }
        }

        private void OnSearchClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel?.Refresh();
        }

        private void OnAddClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var newSnippet = new Models.CodeFile { Title = "New Snippet", Language = "Python" };
            ViewModel?.Snippets.Add(newSnippet);
            if (ViewModel != null)
            {
                ViewModel.SelectedSnippet = newSnippet;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CodeViewModel.SelectedSnippet))
            {
                _ = SendSelectedSnippetToEditorAsync();
                try
                {
                    var root = this.Content as FrameworkElement;
                    var editorPanel = root?.FindName("EditorPanel") as Grid;
                    var cardPanel = root?.FindName("CardPanel") as Grid;
                    var placeholder = root?.FindName("PlaceholderBorder") as Border;
                    if (ViewModel?.SelectedSnippet != null)
                    {
                        if (editorPanel != null) editorPanel.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                        if (cardPanel != null) cardPanel.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                        if (placeholder != null) placeholder.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                    }
                    else
                    {
                        if (editorPanel != null) editorPanel.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                        if (cardPanel != null) cardPanel.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                        if (placeholder != null) placeholder.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                    }
                }
                catch { }
            }
        }

        private async Task SendSelectedSnippetToEditorAsync()
        {
            if (CodeEditor == null) return;
            var vm = ViewModel;
            if (vm?.SelectedSnippet == null) return;
            try
            {
                // Ensure CoreWebView2 is created
                try { await CodeEditor.EnsureCoreWebView2Async(); } catch { }

                var payload = new
                {
                    action = "load",
                    payload = new
                    {
                        id = vm.SelectedSnippet.Id,
                        title = vm.SelectedSnippet.Title,
                        language = vm.SelectedSnippet.Language,
                        content = vm.SelectedSnippet.Content
                    }
                };
                var json = JsonSerializer.Serialize(payload);
                if (CodeEditor.CoreWebView2 != null)
                {
                    // attach handler once
                    if (!_webMessageHooked)
                    {
                        CodeEditor.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                        _webMessageHooked = true;
                    }

                    CodeEditor.CoreWebView2.PostWebMessageAsJson(json);
                }
            }
            catch { }
        }
    }
}


