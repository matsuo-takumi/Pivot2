using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HelixToolkit.SharpDX.Core.Model.Scene;

namespace Pivot.Services
{
    public interface IModelLoaderService
    {
        Task<SceneNode?> LoadModelAsync(string filePath);
        bool IsSupportedFormat(string filePath);
    }

    public class ModelLoaderService : IModelLoaderService
    {
        private readonly ILogger<ModelLoaderService>? _logger;
        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".obj", ".fbx", ".gltf", ".glb", ".dae", ".3ds", ".stl", ".ply"
        };

        public ModelLoaderService(ILogger<ModelLoaderService>? logger = null)
        {
            _logger = logger;
        }

        public bool IsSupportedFormat(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            var ext = Path.GetExtension(filePath);
            return SupportedExtensions.Contains(ext);
        }

        public async Task<SceneNode?> LoadModelAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                _logger?.LogWarning("Model file not found: {FilePath}", filePath);
                return null;
            }

            if (!IsSupportedFormat(filePath))
            {
                _logger?.LogWarning("Unsupported model format: {FilePath}", filePath);
                return null;
            }

            try
            {
                return await Task.Run(() =>
                {
                    var ext = Path.GetExtension(filePath).ToLowerInvariant();
                    SceneNode? model = null;

                    switch (ext)
                    {
                        case ".obj":
                            model = LoadObjModel(filePath);
                            break;
                        case ".stl":
                            model = LoadStlModel(filePath);
                            break;
                        case ".3ds":
                        case ".fbx":
                        case ".dae":
                        case ".gltf":
                        case ".glb":
                        case ".ply":
                            // これらの形式はHelixToolkitのImporterを使用
                            model = LoadWithImporter(filePath);
                            break;
                        default:
                            _logger?.LogWarning("Unsupported file extension: {Extension}", ext);
                            break;
                    }

                    return model;
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading model: {FilePath}", filePath);
                return null;
            }
        }

        private SceneNode? LoadObjModel(string filePath)
        {
            // OBJファイルはImporterで読み込む
            return LoadWithImporter(filePath);
        }

        private SceneNode? LoadStlModel(string filePath)
        {
            // STLファイルはImporterで読み込む
            return LoadWithImporter(filePath);
        }

        private SceneNode? LoadWithImporter(string filePath)
        {
            try
            {
                // HelixToolkitのImporterを使用
                // 注意: HelixToolkit.WinUIの実際のAPIに合わせて調整が必要な場合があります
                // 暫定的に、基本的なファイル読み込みを実装
                // 実際のHelixToolkit.WinUIのAPIに合わせて修正が必要です
                _logger?.LogWarning("Model loading with importer not yet fully implemented for: {FilePath}", filePath);
                
                // TODO: HelixToolkit.WinUIの実際のImporter APIを使用して実装
                // 例: var importer = new HelixToolkit.WinUI.Assimp.Importer();
                //     var model = importer.Load(filePath);
                
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading model with importer: {FilePath}", filePath);
            }
            return null;
        }
    }
}

