using System;
using System.Collections.Generic;
using System.IO;
using Pivot.Models;

namespace Pivot.Services
{
	public static class FileTypeHelper
	{
		private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tga", ".tif", ".tiff", ".webp", ".ico"
		};

		private static readonly HashSet<string> ModelExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			".fbx", ".obj", ".usd", ".usdz", ".gltf", ".glb", ".hdr", ".exr"
		};

		private static readonly HashSet<string> ScriptExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			".py", ".vex", ".mel", ".lua", ".cs", ".cpp", ".h", ".txt"
		};

		public static AssetKind GetKindByExtension(string? pathOrExt)
		{
			if (string.IsNullOrWhiteSpace(pathOrExt)) return AssetKind.Other;
			var ext = pathOrExt.StartsWith(".") ? pathOrExt : Path.GetExtension(pathOrExt);
			if (string.IsNullOrWhiteSpace(ext)) return AssetKind.Other;
			if (ImageExtensions.Contains(ext)) return AssetKind.Image;
			if (ModelExtensions.Contains(ext)) return AssetKind.Model;
			if (ScriptExtensions.Contains(ext)) return AssetKind.Script;
			return AssetKind.Other;
		}
	}
}


