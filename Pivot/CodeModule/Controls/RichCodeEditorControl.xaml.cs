using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pivot.CodeModule.Helpers;
using Pivot.Services;
using Pivot.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Linq;
using Windows.UI;

namespace Pivot.CodeModule.Controls
{
    public sealed partial class RichCodeEditorControl : UserControl
    {
        private bool _isUpdatingText = false;
        private bool _isApplyingHighlight = false;
        private DispatcherTimer _highlightTimer;

        public RichCodeEditorControl()
        {
            this.InitializeComponent();
            
            _highlightTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _highlightTimer.Tick += HighlightTimer_Tick;
            
            Editor.TextChanged += Editor_TextChanged;
            Editor.KeyDown += Editor_KeyDown;
            
            // Listen for theme changes
            WeakReferenceMessenger.Default.Register<CodeThemeChangedMessage>(this, (r, m) =>
            {
                DispatcherQueue.TryEnqueue(() => ApplyHighlighting());
            });
            
            this.Unloaded += (s, e) =>
            {
                WeakReferenceMessenger.Default.Unregister<CodeThemeChangedMessage>(this);
            };
        }

        #region Dependency Properties

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(RichCodeEditorControl),
                new PropertyMetadata(string.Empty, OnTextChanged));

        public static readonly DependencyProperty LanguageProperty =
            DependencyProperty.Register(
                nameof(Language),
                typeof(string),
                typeof(RichCodeEditorControl),
                new PropertyMetadata("plaintext", OnLanguageChanged));

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(
                nameof(IsReadOnly),
                typeof(bool),
                typeof(RichCodeEditorControl),
                new PropertyMetadata(false, OnIsReadOnlyChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string Language
        {
            get => (string)GetValue(LanguageProperty);
            set => SetValue(LanguageProperty, value);
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
            var control = (RichCodeEditorControl)d;
            if (!control._isUpdatingText && e.NewValue is string newText)
            {
                control.SetEditorText(newText);
            }
        }

        private static void OnLanguageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (RichCodeEditorControl)d;
            control.ApplyHighlighting();
        }

        private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (RichCodeEditorControl)d;
            if (e.NewValue is bool isReadOnly)
            {
                control.Editor.IsReadOnly = isReadOnly;
            }
        }

        #endregion

        private void Editor_TextChanged(object sender, RoutedEventArgs e)
        {
            if (_isApplyingHighlight) return;

            _isUpdatingText = true;
            Editor.Document.GetText(TextGetOptions.None, out string text);
            Text = text;
            _isUpdatingText = false;

            // Debounce highlighting
            _highlightTimer.Stop();
            _highlightTimer.Start();
        }

        private void HighlightTimer_Tick(object sender, object e)
        {
            _highlightTimer.Stop();
            ApplyHighlighting();
        }

        private void SetEditorText(string text)
        {
            _isApplyingHighlight = true;
            Editor.Document.SetText(TextSetOptions.None, text ?? string.Empty);
            _isApplyingHighlight = false;
            ApplyHighlighting();
        }

        private void ApplyHighlighting()
        {
            if (string.IsNullOrEmpty(Language) || Language == "plaintext")
                return;

            _isApplyingHighlight = true;

            try
            {
                Editor.Document.GetText(TextGetOptions.None, out string text);
                var tokens = SyntaxHighlighter.Tokenize(text, Language);

                // Get colors from settings
                var themeSettings = App.Current.Services.GetService(typeof(ThemeSettingsService)) as ThemeSettingsService;
                var colors = GetTokenColors(themeSettings);

                // Apply formatting
                foreach (var token in tokens)
                {
                    var range = Editor.Document.GetRange(token.Start, token.Start + token.Length);
                    
                    Color color = token.Type switch
                    {
                        TokenType.Keyword => colors.Keyword,
                        TokenType.String => colors.String,
                        TokenType.Comment => colors.Comment,
                        TokenType.Number => colors.Number,
                        _ => colors.Foreground
                    };

                    range.CharacterFormat.ForegroundColor = color;
                }

                // Set background
                Editor.Background = new SolidColorBrush(colors.Background);
            }
            catch
            {
                // Ignore errors during highlighting
            }
            finally
            {
                _isApplyingHighlight = false;
            }
        }

        private void Editor_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                HandleEnterKey(e);
            }
            else if (e.Key == Windows.System.VirtualKey.Tab)
            {
                HandleTabKey(e);
            }
        }

        private void HandleEnterKey(Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            try
            {
                // Get current line
                Editor.Document.Selection.GetIndex(TextRangeUnit.Paragraph, out int lineIndex);
                var lineRange = Editor.Document.GetRange(0, 0);
                lineRange.Move(TextRangeUnit.Paragraph, lineIndex);
                lineRange.EndOf(TextRangeUnit.Paragraph, false);
                lineRange.GetText(TextGetOptions.None, out string currentLine);

                // Calculate indent
                int indentCount = 0;
                foreach (char c in currentLine)
                {
                    if (c == ' ') indentCount++;
                    else if (c == '\t') indentCount += 4;
                    else break;
                }

                // Check if line ends with opening brace
                bool addExtraIndent = currentLine.TrimEnd().EndsWith("{") || 
                                     currentLine.TrimEnd().EndsWith(":");

                // Don't prevent default, just insert indent after
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, () =>
                {
                    int totalIndent = indentCount + (addExtraIndent ? 4 : 0);
                    string indent = new string(' ', totalIndent);
                    Editor.Document.Selection.SetText(TextSetOptions.None, indent);
                    Editor.Document.Selection.StartPosition = Editor.Document.Selection.EndPosition;
                });
            }
            catch { }
        }

        private void HandleTabKey(Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            e.Handled = true;
            
            try
            {
                // Insert 4 spaces instead of tab
                Editor.Document.Selection.SetText(TextSetOptions.None, "    ");
                Editor.Document.Selection.StartPosition = Editor.Document.Selection.EndPosition;
            }
            catch { }
        }

        private (Color Background, Color Foreground, Color Keyword, Color String, Color Comment, Color Number) GetTokenColors(ThemeSettingsService? themeSettings)
        {
            Color GetColor(string key, Color defaultColor)
            {
                if (themeSettings == null) return defaultColor;
                
                var def = CodeColorRoleDefinitions.Roles.FirstOrDefault(r => r.SettingKey == key);
                if (def == null) return defaultColor;
                
                var hex = themeSettings.GetTextColorOverride(key, def.DefaultHex);
                return Pivot.Utilities.TextColorHelper.ParseHexOrDefault(hex, def.DefaultColor);
            }

            return (
                Background: GetColor("Code.Background", ColorHelper.FromArgb(255, 30, 30, 30)),
                Foreground: GetColor("Code.Foreground", ColorHelper.FromArgb(255, 212, 212, 212)),
                Keyword: GetColor("Code.Keyword", ColorHelper.FromArgb(255, 197, 134, 192)),
                String: GetColor("Code.String", ColorHelper.FromArgb(255, 206, 145, 120)),
                Comment: GetColor("Code.Comment", ColorHelper.FromArgb(255, 106, 153, 85)),
                Number: GetColor("Code.Number", ColorHelper.FromArgb(255, 181, 206, 168))
            );
        }
    }
}
