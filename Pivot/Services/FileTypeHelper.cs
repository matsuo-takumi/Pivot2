using System;
using System.Collections.Generic;
using System.IO;
using Pivot.Engine.Models;

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
			// 標準フォーマット
			".fbx", ".obj", ".glb", ".gltf", ".dae",
			// 業界標準
			".3ds", ".blend", ".stl", ".ply", ".x",
			// CAD/製造
			".dxf", ".ifc", ".3mf",
			// ゲーム向け
			".md2", ".md3", ".md5mesh", ".mdl", ".smd",
			// その他
			".lwo", ".lxo", ".ase", ".ac", ".b3d", ".ogex",
			// 追加フォーマット
			".ms3d", ".cob", ".scn", ".bvh", ".irrmesh", ".nff", ".off", ".raw", ".ter"
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


