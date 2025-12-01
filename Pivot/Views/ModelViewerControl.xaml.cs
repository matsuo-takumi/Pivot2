using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Pivot.ViewModels;
using HelixToolkit.SharpDX.Core.Model.Scene;
using Microsoft.Extensions.DependencyInjection;
using Pivot.Services;

namespace Pivot.Views
{
    public sealed partial class ModelViewerControl : UserControl
    {
        public ModelViewModel ViewModel { get; private set; }

        public ModelViewerControl()
        {
            this.InitializeComponent();
            
            // ViewModelをDIから取得または作成
            try
            {
                ViewModel = App.Current.Services.GetService<ModelViewModel>();
                if (ViewModel == null)
                {
                    // フォールバック: 直接インスタンス化
                    var modelLoaderService = App.Current.Services.GetService<IModelLoaderService>();
                    if (modelLoaderService == null)
                    {
                        modelLoaderService = new ModelLoaderService();
                    }
                    ViewModel = new ModelViewModel(modelLoaderService);
                }
            }
            catch
            {
                // フォールバック
                ViewModel = new ModelViewModel(new ModelLoaderService());
            }

            this.DataContext = ViewModel;
            
            // ViewModelの変更を監視してビューポートを更新
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ModelViewModel.CurrentModel))
            {
                UpdateViewport();
            }
        }

        private void UpdateViewport()
        {
            // TODO: HelixToolkit.WinUIの実際のAPIに合わせて実装
            // 現在は暫定的な実装
            // HelixToolkit.WinUIの正しいAPIが確認でき次第、実装を更新する必要があります
        }

        public async Task LoadModelAsync(string filePath)
        {
            if (ViewModel != null)
            {
                await ViewModel.LoadModelCommand.ExecuteAsync(filePath);
            }
        }

        public void ClearModel()
        {
            ViewModel?.ClearModelCommand.Execute(null);
        }
    }
}

