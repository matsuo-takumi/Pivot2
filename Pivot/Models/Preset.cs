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
}

