using System;
using System.Collections.Generic;

namespace Pivot.Models
{
    /// <summary>
    /// 汎用的なプリセットデータモデル
    /// </summary>
    public class Preset<T>
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public T Data { get; set; } = default!;
    }

    /// <summary>
    /// テキストカラー設定用のプリセットデータ
    /// </summary>
    public class TextColorPresetData
    {
        public Dictionary<string, string> ColorOverrides { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// コードエディタ配色設定用のプリセットデータ
    /// </summary>
    public class CodeColorPresetData
    {
        // Settings
        public bool IsCustomizationEnabled { get; set; }
        public string BackgroundMode { get; set; } = "Theme"; // Theme or Custom
        public string CustomBackgroundColor { get; set; } = "";

        // Colors (Nullable hex strings)
        public string? EditorTextColor { get; set; }
        public string? EditorBackgroundColor { get; set; }
        public string? TitleColor { get; set; }
        public string? TagTextColor { get; set; }
        public string? TagBackgroundColor { get; set; }
        public string? TagBorderColor { get; set; }
        public string? LineNumberColor { get; set; }
    }
}





