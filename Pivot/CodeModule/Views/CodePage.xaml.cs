using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Pivot.CodeModule.ViewModels;
using Pivot.CodeModule.Controls;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Pivot.Models;

namespace Pivot.CodeModule.Views
{
    public sealed partial class CodePage : Page
    {
        public CodeViewModel ViewModel { get; private set; } = null!;

        public CodePage()
        {
            this.InitializeComponent();
            
            // Register Keyboard Accelerators
            var saveAccelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
            {
                Modifiers = Windows.System.VirtualKeyModifiers.Control,
                Key = Windows.System.VirtualKey.S
            };
            saveAccelerator.Invoked += SaveAccelerator_Invoked;
            this.KeyboardAccelerators.Add(saveAccelerator);
            // Resolve ViewModel from App Services
            ViewModel = ((App)Application.Current).Services.GetService<CodeViewModel>() 
                ?? throw new InvalidOperationException("CodeViewModel not registered in DI");
            this.DataContext = ViewModel;
        }

        private async void SaveAccelerator_Invoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            if (ViewModel?.EditorVM?.IsEditing == true)
            {
                await ViewModel.EditorVM.SaveContentAsync();
            }
            else
            {
                // Trigger Quick Add Save if it's open
                QuickAddBar?.TrySave();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // Trigger initial load
            if (ViewModel != null)
            {
               _ = InitializePageAsync();
            }
        }

        private async Task InitializePageAsync()
        {
            await ViewModel.InitializeAsync();
            
            // Pass available tags to QuickAddControl
            UpdateQuickAddTags();
        }

        private void UpdateQuickAddTags()
        {
            if (ViewModel?.FilterVM?.Tags != null)
            {
                QuickAddBar.SetAvailableTags(ViewModel.FilterVM.Tags);
                ViewModel.EditorVM?.SetAvailableTags(ViewModel.FilterVM.Tags);
            }
        }

        private async void QuickAddBar_SnippetCreated(object sender, QuickAddEventArgs e)
        {
            if (ViewModel?.EditorVM == null) return;

            // Create snippet via EditorVM
            var newSnippet = await ViewModel.EditorVM.CreateSnippetAsync(
                e.Title, 
                e.Language, 
                e.Code, 
                e.Tags);

            if (newSnippet != null)
            {
                // Small delay to ensure DB transaction commits
                await Task.Delay(100);
                
                // Refresh list to show new item
                await ViewModel.ListVM.LoadSnippetsAsync();
                // Also refresh tags
                ViewModel.FilterVM.LoadTags(ViewModel.ListVM.Snippets);
                // Update QuickAddControl with new tags
                UpdateQuickAddTags();
            }
        }

        private async void SnippetCard_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // SnippetCardControl uses Asset dependency property, not DataContext
            if (sender is SnippetCardControl card && card.Asset != null)
            {
                await ViewModel.EditorVM.SetSnippetAsync(card.Asset);
            }
        }
    }
}
