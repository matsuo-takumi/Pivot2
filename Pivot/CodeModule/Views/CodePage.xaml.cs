using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Pivot.CodeModule.ViewModels;
using Pivot.Models;
using Pivot.CodeModule.Controls;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Pivot.Engine.Models;

namespace Pivot.CodeModule.Views
{
    public sealed partial class CodePage : Page
    {
        public CodeViewModel ViewModel { get; private set; } = null!;
        private QuickAddViewModel QuickAddVM { get; set; } = null!;

        public CodePage()
        {
            // Resolve ViewModels from App Services BEFORE InitializeComponent
            var services = ((App)Application.Current).Services;
            ViewModel = services.GetService<CodeViewModel>() 
                ?? throw new InvalidOperationException("CodeViewModel not registered in DI");
            QuickAddVM = services.GetService<QuickAddViewModel>()
                ?? throw new InvalidOperationException("QuickAddViewModel not registered in DI");
            
            this.InitializeComponent();
            
            this.DataContext = ViewModel;
            
            // Set QuickAddBar's DataContext
            QuickAddBar.DataContext = QuickAddVM;
            
            // Subscribe to QuickAddViewModel's SnippetCreated event
            QuickAddVM.SnippetCreated += QuickAddBar_SnippetCreated;

            // Register Keyboard Accelerators
            var saveAccelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
            {
                Modifiers = Windows.System.VirtualKeyModifiers.Control,
                Key = Windows.System.VirtualKey.S
            };
            saveAccelerator.Invoked += SaveAccelerator_Invoked;
            this.KeyboardAccelerators.Add(saveAccelerator);
        }

        private Windows.UI.Text.FontWeight GetAllButtonFontWeight(string? selectedTag)
        {
            return string.IsNullOrEmpty(selectedTag) ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
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
                QuickAddBar.TrySave();
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.InitializeAsync();
            
            // Update QuickAdd tags after initialization
            UpdateQuickAddTags();
        }

        private void AllButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.FilterVM.SelectedTag = null;
        }

        private void SnippetCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is AssetModel snippet)
            {
                _ = ViewModel.EditorVM.SetSnippetAsync(snippet.Entity);
            }
        }

        private async void QuickAddBar_SnippetCreated(object? sender, QuickAddEventArgs e)
        {
            // Delegate to CodeViewModel's orchestration method
            await ViewModel.OnSnippetCreatedAsync(e);
            
            // Update QuickAdd tags after creation
            UpdateQuickAddTags();
        }

        private void UpdateQuickAddTags()
        {
            QuickAddVM.SetAvailableTags(ViewModel.FilterVM.Tags);
        }

        private async void SnippetsGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AssetModel asset)
            {
                await ViewModel.EditorVM.SetSnippetAsync(asset.Entity);
            }
        }
    }
}
