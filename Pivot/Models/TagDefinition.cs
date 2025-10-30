using System.Collections.Generic;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Linq;

namespace Pivot.Models
{
    /// <summary>
    /// ユーザー定義のタグ（名前と拡張子リスト）。
    /// 拡張子はドット無し・小文字で格納することを想定します。
    /// </summary>
    public partial class TagDefinition : ObservableObject
    {
        private string _name = string.Empty;
        private List<string> _extensions = new List<string>();
        private string? _colorHex;

        /// <summary>タグ名（例: "Models", "Textures"）</summary>
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        /// <summary>関連付ける拡張子一覧（例: "obj", "fbx", "jpg"）</summary>
        public List<string> Extensions { get => _extensions; set => SetProperty(ref _extensions, value); }

        /// <summary>任意: UI 表示用のカラー（例: "#FF8A00"）。NULL 可。</summary>
        public string? ColorHex { get => _colorHex; set => SetProperty(ref _colorHex, value); }

        /// <summary>UI 用: 編集モードかどうか。永続化しない。</summary>
        [JsonIgnore]
        private bool _isEditing = false;

        [JsonIgnore]
        public bool IsEditing { get => _isEditing; set => SetProperty(ref _isEditing, value); }

        /// <summary>UI 用: 編集開始時の元の名前を保持（永続化しない）</summary>
        [JsonIgnore]
        public string? OriginalName { get; set; }

        /// <summary>UI 用: 拡張子をカンマ区切りで扱うプロパティ（永続化しない）</summary>
        [JsonIgnore]
        public string ExtensionsCsv
        {
            get => string.Join(",", Extensions ?? new List<string>());
            set
            {
                var list = (value ?? string.Empty).Split(',')
                    .Select(s => s.Trim().TrimStart('.').ToLowerInvariant())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct()
                    .ToList();
                Extensions = list;
                OnPropertyChanged(nameof(ExtensionsCsv));
            }
        }
    }
}
