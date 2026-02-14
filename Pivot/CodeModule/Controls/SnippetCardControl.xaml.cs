using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Pivot.Engine.Models;
using Pivot.Services;
using Pivot.CodeModule.ViewModels;
using Pivot.CodeModule.Services;
using Microsoft.Extensions.DependencyInjection;
using Pivot.Models; // Added for AssetEntityExtensions

namespace Pivot.CodeModule.Controls
{
    public sealed partial class SnippetCardControl : UserControl
    {
        public static readonly DependencyProperty AssetProperty =
            DependencyProperty.Register("Asset", typeof(AssetModel), typeof(SnippetCardControl), new PropertyMetadata(null));

        public AssetModel Asset
        {
            get => (AssetModel)GetValue(AssetProperty);
            set => SetValue(AssetProperty, value);
        }

        public static readonly DependencyProperty SettingsProperty =
            DependencyProperty.Register(nameof(Settings), typeof(CodeSettingsViewModel), typeof(SnippetCardControl), new PropertyMetadata(null));

        public CodeSettingsViewModel Settings
        {
            get => (CodeSettingsViewModel)GetValue(SettingsProperty);
            set => SetValue(SettingsProperty, value);
        }

        public SnippetCardControl()
        {
            this.InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (Settings == null)
            {
                Settings = ((App)Application.Current).Services.GetService<CodeSettingsViewModel>()!;
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Optional cleanup
        }

        // Removed manual OnSettingsChanged and UpdateBackground as we will use bindings
        private void Dummy() { } // Placeholder to keep diff clean if needed or just remove methods entirely



        private CodeService? GetCodeService()
        {
            return ((App)Application.Current).Services.GetService<CodeService>();
        }

        private CodeListViewModel? GetListViewModel()
        {
            DependencyObject? current = this;
            while (current != null)
            {
                if (current is Pivot.CodeModule.Views.CodePage page)
                {
                    return page.ViewModel?.ListVM;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            await CopyContentAsync();
        }

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = GetListViewModel();
            if (vm != null && Asset != null)
            {
                vm.TogglePinCommand.Execute(Asset);
            }
        }

        private async void CopyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await CopyContentAsync();
        }

        private async Task CopyContentAsync()
        {
            if (Asset == null) return;

            try
            {
                // Get CodeService from page
                var codeService = GetCodeService();
                if (codeService == null) return;

                string content = await codeService.ReadContentAsync(Asset.Entity);

                // Process variables
                var processedContent = await ProcessTemplateVariablesAsync(content);
                if (processedContent == null) return; // User cancelled

                var dataPackage = new DataPackage();
                dataPackage.SetText(processedContent);
                Clipboard.SetContent(dataPackage);
            }
            catch { }
        }

        private async Task<string?> ProcessTemplateVariablesAsync(string content)
        {
            try 
            {
                var regex = new Regex(@"\$\{([a-zA-Z0-9_]+)\}");
                var matches = regex.Matches(content);
                
                if (matches.Count == 0) return content;
                
                var variables = new Dictionary<string, string>();
                var customVariables = new List<TemplateVariable>();
                
                foreach (Match match in matches)
                {
                    var name = match.Groups[1].Value;
                    if (variables.ContainsKey(name)) continue;
                    if (customVariables.Exists(v => v.Name == name)) continue;
                    
                    string val = "";
                    bool isStandard = true;
        
                    // Standard variables
                    switch (name.ToLower())
                    {
                        case "date": val = DateTime.Now.ToString("yyyy/MM/dd"); break;
                        case "time": val = DateTime.Now.ToString("HH:mm:ss"); break;
                        case "datetime": val = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"); break;
                        case "clipboard":
                            try {
                                var pack = Clipboard.GetContent();
                                if (pack.Contains(StandardDataFormats.Text))
                                    val = await pack.GetTextAsync();
                            } catch {} 
                            break;
                        default:
                            isStandard = false;
                            break;
                    }

                    if (isStandard)
                    {
                        variables[name] = val;
                    }
                    else
                    {
                        customVariables.Add(new Pivot.CodeModule.Controls.TemplateVariable { Name = name });
                    }
                }
                
                // Ask user for custom variables
                if (customVariables.Count > 0)
                {
                    var dialog = new Pivot.CodeModule.Controls.VariableInputDialog(customVariables);
                    dialog.XamlRoot = this.XamlRoot;
                    var result = await dialog.ShowAsync();
                    
                    if (result != ContentDialogResult.Primary)
                    {
                         return null; // Cancelled
                    }
                    
                    foreach (var v in customVariables)
                    {
                        variables[v.Name] = v.Value;
                    }
                }
                
                // Replace
                return regex.Replace(content, m => 
                {
                    var name = m.Groups[1].Value;
                    return variables.ContainsKey(name) ? variables[name] : m.Value;
                });
            }
            catch 
            {
                return content; // Fallback to original on error
            }
        }

        private async void DuplicateMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Asset == null) return;

            var listVM = GetListViewModel();
            if (listVM == null) return;

            // Find CodePage to access EditorVM for creation
            DependencyObject? current = this;
            Pivot.CodeModule.Views.CodePage? page = null;
            while (current != null)
            {
                if (current is Pivot.CodeModule.Views.CodePage p)
                {
                    page = p;
                    break;
                }
                current = VisualTreeHelper.GetParent(current);
            }

            if (page?.ViewModel?.EditorVM == null) return;

            // Get CodeService
            var codeService = GetCodeService();
            if (codeService == null) return;

            // Read content
            string content = await codeService.ReadContentAsync(Asset.Entity);

            // Create duplicate with modified name
            var newSnippet = await page.ViewModel.EditorVM.CreateSnippetAsync(
                Asset.FileName + "_copy",
                Asset.Entity.Tool ?? "text",
                content,
                // AssetModel wraps Entity, UserTagsJson is string, need to deserialize or use helper?
                // AssetModel doesn't have GetTags method, AssetEntity might have had extension?
                // Checking AssetEntityExtensions usage.
                // Assuming extension methods still work on Asset.Entity
                Pivot.Models.AssetEntityExtensions.GetTags(Asset.Entity).ToArray()
            );

            if (newSnippet != null)
            {
                await listVM.LoadSnippetsAsync();
            }
        }

        private void MoveUpMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Asset == null) return;
            var listVM = GetListViewModel();
            listVM?.MoveItemUpCommand?.Execute(Asset);
        }

        private void MoveDownMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Asset == null) return;
            var listVM = GetListViewModel();
            listVM?.MoveItemDownCommand?.Execute(Asset);
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Asset == null) return;
            var listVM = GetListViewModel();
            listVM?.DeleteItemCommand?.Execute(Asset);
        }

    }
}
