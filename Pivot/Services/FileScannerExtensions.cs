using Pivot.Models;

namespace Pivot.Services
{
	public static class FileScannerExtensions
	{
		public static AssetKind DetermineAssetKind(string extension)
		{
			if (string.IsNullOrEmpty(extension)) return AssetKind.General;

			return extension.ToLowerInvariant() switch
			{
				".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tga" or ".tif" or ".tiff" or ".webp" => AssetKind.Image,
				".mp4" or ".mov" or ".avi" or ".mkv" or ".webm" => AssetKind.Video,
				".fbx" or ".obj" or ".usd" or ".usdz" or ".gltf" or ".glb" => AssetKind.Model3D,
				".cs" or ".py" or ".js" or ".ts" or ".cpp" or ".h" or ".hlsl" or ".glsl" or
				".json" or ".md" or ".xml" or ".txt" or ".bat" or ".ps1" or ".sh" or ".sql" or ".yaml" or ".yml" or ".css" or ".html" => AssetKind.Script,
				".uproject" or ".sln" or ".csproj" => AssetKind.Project,
				_ => AssetKind.General
			};
		}
	}
}
