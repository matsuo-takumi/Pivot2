using System;
using System.Collections.Generic;

namespace Pivot.Models
{
    public class ScriptEntry
    {
        public int Id { get; set; }

        /// <summary>
        /// スクリプトの名前（例: "MayaRigBuilder", "HoudiniVEXTool"）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// スクリプト言語の種類（VEX, Python, PyMEL, C++, Lua など）
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// スクリプトファイルの絶対パス
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// スクリプトコードの内容（またはコード内容のハッシュ値）
        /// </summary>
        public string CodeContent { get; set; }

        /// <summary>
        /// コードのハッシュ値（変更検知用）
        /// </summary>
        public string Hash { get; set; }

        /// <summary>
        /// スクリプトが対応しているアプリケーション
        /// （例: "Houdini", "Maya", "Blender", "UnrealEngine", "Substance" など）
        /// </summary>
        public string TargetApplication { get; set; }

        /// <summary>
        /// スクリプトのバージョン
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// スクリプトの説明・ドキュメント
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// スクリプトの分類カテゴリ（例: "Rigging", "Animation", "DataProcessing", "Utility"）
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// スクリプトのタグ（JSON形式のリスト）
        /// </summary>
        public string TagsJson { get; set; }

        /// <summary>
        /// スクリプトが依存する他のスクリプト ID のリスト（JSON形式）
        /// </summary>
        public string DependenciesJson { get; set; }

        /// <summary>
        /// スクリプトの入力パラメータ定義（JSON形式）
        /// 例: [{"name": "inputPath", "type": "string", "required": true}]
        /// </summary>
        public string InputParametersJson { get; set; }

        /// <summary>
        /// スクリプトが実行可能かどうかのフラグ
        /// </summary>
        public bool IsExecutable { get; set; }

        /// <summary>
        /// スクリプトが有効かどうかのフラグ（無効化したスクリプトを保持する用）
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// ファイルサイズ（バイト）
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// スクリプト作成日時
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// スクリプト更新日時
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// スクリプト作成者
        /// </summary>
        public string Author { get; set; }

        public ScriptEntry()
        {
            Name = string.Empty;
            Language = string.Empty;
            Path = string.Empty;
            CodeContent = string.Empty;
            Hash = string.Empty;
            TargetApplication = string.Empty;
            Version = "1.0.0";
            Description = string.Empty;
            Category = string.Empty;
            TagsJson = "[]";
            DependenciesJson = "[]";
            InputParametersJson = "[]";
            IsExecutable = false;
            IsActive = true;
            Author = string.Empty;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            Size = 0;
        }
    }
}
