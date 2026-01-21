using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Pivot.CodeModule.Helpers;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Pivot.CodeModule.Controls
{
    /// <summary>
    /// Reusable Monaco Editor control with two-way binding support.
    /// </summary>
    public sealed partial class MonacoEditorControl : UserControl
    {
        private bool _isMonacoReady = false;
        private string _pendingContent = string.Empty;
        private string _pendingLanguage = "plaintext";
        private bool _isUpdatingFromMonaco = false;

        public MonacoEditorControl()
        {
            this.InitializeComponent();
            this.Loaded += OnLoaded;
        }

        #region Dependency Properties

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(MonacoEditorControl),
                new PropertyMetadata(string.Empty, OnTextChanged));

        public static readonly DependencyProperty EditorLanguageProperty =
            DependencyProperty.Register(
                nameof(EditorLanguage),
                typeof(string),
                typeof(MonacoEditorControl),
                new PropertyMetadata("plaintext", OnEditorLanguageChanged));

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(
                nameof(IsReadOnly),
                typeof(bool),
                typeof(MonacoEditorControl),
                new PropertyMetadata(false, OnIsReadOnlyChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string EditorLanguage
        {
            get => (string)GetValue(EditorLanguageProperty);
            set => SetValue(EditorLanguageProperty, value);
        }

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        #endregion

        #region Property Changed Handlers

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (MonacoEditorControl)d;
            if (!control._isUpdatingFromMonaco && e.NewValue is string newText)
            {
                control.SetMonacoContent(newText, control.EditorLanguage);
            }
        }

        private static void OnEditorLanguageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (MonacoEditorControl)d;
            if (e.NewValue is string newLanguage)
            {
                control.SetMonacoLanguage(newLanguage);
            }
        }

        private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (MonacoEditorControl)d;
            if (e.NewValue is bool isReadOnly)
            {
                control.SetMonacoReadOnly(isReadOnly);
            }
        }

        #endregion

        #region Monaco Initialization

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await InitializeMonacoAsync();
        }

        private async Task InitializeMonacoAsync()
        {
            try
            {
                await MonacoWebView.EnsureCoreWebView2Async();

                // Set up message handler
                MonacoWebView.WebMessageReceived += MonacoWebView_WebMessageReceived;

                // Navigate to local Monaco HTML
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var monacoPath = Path.Combine(appDir, "Assets", "Monaco", "editor.html");

                if (File.Exists(monacoPath))
                {
                    MonacoWebView.Source = new Uri(monacoPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Monaco init failed: {ex.Message}");
            }
        }

        private void MonacoWebView_WebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                var message = JsonSerializer.Deserialize<MonacoMessage>(args.WebMessageAsJson);
                if (message?.type == "ready")
                {
                    _isMonacoReady = true;
                    
                    // Set pending content if any
                    if (!string.IsNullOrEmpty(_pendingContent) || !string.IsNullOrEmpty(Text))
                    {
                        SetMonacoContent(_pendingContent ?? Text, _pendingLanguage ?? EditorLanguage);
                    }
                    
                    // Set read-only state
                    SetMonacoReadOnly(IsReadOnly);
                }
                else if (message?.type == "contentChanged")
                {
                    _isUpdatingFromMonaco = true;
                    Text = message.content ?? string.Empty;
                    _isUpdatingFromMonaco = false;
                }
            }
            catch { }
        }

        #endregion

        #region Monaco Control Methods

        private async void SetMonacoContent(string content, string language)
        {
            if (!_isMonacoReady || MonacoWebView.CoreWebView2 == null)
            {
                _pendingContent = content;
                _pendingLanguage = language;
                return;
            }

            try
            {
                var escapedContent = JsonSerializer.Serialize(content);
                await MonacoWebView.ExecuteScriptAsync($"window.monacoApi.setContent({escapedContent})");
                await MonacoWebView.ExecuteScriptAsync($"window.monacoApi.setLanguage('{language}')");
                
                _pendingContent = string.Empty;
            }
            catch { }
        }

        private async void SetMonacoLanguage(string language)
        {
            if (!_isMonacoReady || MonacoWebView.CoreWebView2 == null)
            {
                _pendingLanguage = language;
                return;
            }

            try
            {
                await MonacoWebView.ExecuteScriptAsync($"window.monacoApi.setLanguage('{language}')");
            }
            catch { }
        }

        private async void SetMonacoReadOnly(bool isReadOnly)
        {
            if (!_isMonacoReady || MonacoWebView.CoreWebView2 == null)
                return;

            try
            {
                var readOnlyStr = isReadOnly ? "true" : "false";
                await MonacoWebView.ExecuteScriptAsync($"window.monacoApi.setReadOnly({readOnlyStr})");
            }
            catch { }
        }

        /// <summary>
        /// Gets the current content from Monaco editor.
        /// </summary>
        public async Task<string> GetContentAsync()
        {
            if (!_isMonacoReady || MonacoWebView.CoreWebView2 == null)
                return Text;

            try
            {
                var result = await MonacoWebView.ExecuteScriptAsync("window.monacoApi.getContent()");
                if (!string.IsNullOrEmpty(result) && result != "null")
                {
                    return JsonSerializer.Deserialize<string>(result) ?? Text;
                }
            }
            catch { }

            return Text;
        }

        #endregion

        private class MonacoMessage
        {
            public string? type { get; set; }
            public string? content { get; set; }
        }
    }
}
