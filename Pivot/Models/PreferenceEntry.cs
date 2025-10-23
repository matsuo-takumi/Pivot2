using System;

namespace Pivot.Models
{
    public class PreferenceEntry
    {
        public string Key { get; set; } // 設定のキー（例: "AppTheme", "AssetDirectories"）
        public string Value { get; set; } // 設定の値（JSON文字列として保存）

        /// <summary>
        /// パラメータなしコンストラクタ。
        /// </summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public PreferenceEntry()
        {
            Key = string.Empty;
            Value = string.Empty;
        }

        /// <summary>
        /// 指定されたキーと値を持つ PreferenceEntry を作成します。
        /// </summary>
        /// <param name="key">設定のキー（null または空文字列は許可されません）</param>
        /// <param name="value">設定の値</param>
        /// <exception cref="ArgumentException">key が null または空文字列の場合</exception>
        public PreferenceEntry(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Preference key cannot be null or empty.", nameof(key));
            
            Key = key;
            Value = value;
        }
    }
}
