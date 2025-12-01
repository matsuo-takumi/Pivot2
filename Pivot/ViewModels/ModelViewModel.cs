using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelixToolkit.SharpDX.Core.Model.Scene;
using Microsoft.Extensions.Logging;
using Pivot.Services;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Pivot.ViewModels
{
    public partial class ModelViewModel : ObservableObject
    {
        private readonly IModelLoaderService _modelLoaderService;
        private readonly ILogger<ModelViewModel>? _logger;

        [ObservableProperty]
        private SceneNode? _currentModel;

        [ObservableProperty]
        private string? _modelFilePath;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private bool _hasError;

        public ModelViewModel(IModelLoaderService modelLoaderService, ILogger<ModelViewModel>? logger = null)
        {
            _modelLoaderService = modelLoaderService ?? throw new ArgumentNullException(nameof(modelLoaderService));
            _logger = logger;
        }

        [RelayCommand]
        public async Task LoadModelAsync(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                ErrorMessage = "ファイルパスが指定されていません";
                HasError = true;
                return;
            }

            IsLoading = true;
            HasError = false;
            ErrorMessage = null;
            CurrentModel = null;
            ModelFilePath = filePath;

            try
            {
                var model = await _modelLoaderService.LoadModelAsync(filePath);
                if (model != null)
                {
                    CurrentModel = model;
                    HasError = false;
                    ErrorMessage = null;
                }
                else
                {
                    ErrorMessage = "モデルの読み込みに失敗しました";
                    HasError = true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading model: {FilePath}", filePath);
                ErrorMessage = $"エラー: {ex.Message}";
                HasError = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public void ClearModel()
        {
            CurrentModel = null;
            ModelFilePath = null;
            HasError = false;
            ErrorMessage = null;
        }
    }
}

