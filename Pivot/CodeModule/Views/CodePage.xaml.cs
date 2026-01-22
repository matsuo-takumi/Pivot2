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
                QuickAddBar?.TrySave();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (ViewModel != null)
            {
               _ = InitializePageAsync();
            }
        }

        private async Task InitializePageAsync()
        {
            await ViewModel.InitializeAsync();
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

            var newSnippet = await ViewModel.EditorVM.CreateSnippetAsync(
                e.Title, 
                e.Language, 
                e.Code, 
                e.Tags);

            if (newSnippet != null)
            {
                await Task.Delay(100);
                await ViewModel.ListVM.LoadSnippetsAsync();
                await ViewModel.FilterVM.LoadTagsAsync();
                UpdateQuickAddTags();
            }
        }

        /// <summary>
        /// GridView item click handler - opens snippet editor
        /// </summary>
        private async void SnippetsGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AssetEntity asset)
            {
                await ViewModel.EditorVM.SetSnippetAsync(asset);
            }
        }
    }
}
