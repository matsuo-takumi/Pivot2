using System;
using System.Collections.Generic;
using LiteDB;

namespace Pivot.Models
{
    /// <summary>
    /// Unreal Engine のシーンプリセット情報を表すモデル
    /// 
    /// BridgeSystem経由でUnreal Engineから受信するプリセット情報、
    /// またはPivot内で管理するUnreal連携プリセットを保持する
    /// </summary>
    public class UnrealPresetEntry
    {
        [BsonId]
        public int Id { get; set; }

        /// <summary>
        /// プリセットの名前（例: "CharacterLighting_Day", "CameraFly_Cinematic"）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// プリセットが関連する .uproject ファイルのパス
        /// </summary>
        public string UprojectPath { get; set; }

        /// <summary>
        /// 対応するUnreal Engineプロジェクト名
        /// </summary>
        public string ProjectName { get; set; }

        /// <summary>
        /// プリセットの説明・ドキュメント
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// プリセットの分類カテゴリ（例: "Lighting", "Camera", "PostProcess", "Landscape", "Animation"）
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// プリセットのタグ（JSON形式のリスト）
        /// </summary>
        public string TagsJson { get; set; }

        /// <summary>
        /// プリセットの実際のデータ内容（JSON形式）
        /// Unrealのシーンプロパティ、設定値などをJSON化したもの
        /// 例: {"Lighting": {...}, "Camera": {...}, "PostProcessing": {...}}
        /// </summary>
        public string PresetDataJson { get; set; }

        /// <summary>
        /// このプリセットが依存するアセット、スクリプト、または他のプリセットのIDリスト（JSON形式）
        /// 例: {"assets": [1, 3, 5], "scripts": [2], "presets": [4]}
        /// </summary>
        public string DependenciesJson { get; set; }

        /// <summary>
        /// プリセットのバージョン
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// プリセットが有効かどうかのフラグ
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// プリセットがUnreal Engine内で適用可能かどうか
        /// （互換性チェック用）
        /// </summary>
        public bool IsCompatible { get; set; }

        /// <summary>
        /// プリセットファイルのハッシュ値（変更検知用）
        /// </summary>
        public string Hash { get; set; }

        /// <summary>
        /// Unrealプロジェクトのエンジンバージョン（例: "5.3", "5.4"）
        /// </summary>
        public string EngineVersion { get; set; }

        /// <summary>
        /// プリセット作成者
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// プリセット作成日時
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// プリセット更新日時
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// 最後にUnrealに同期された日時
        /// </summary>
        public DateTime? LastSyncedAt { get; set; }

        public UnrealPresetEntry()
        {
            Name = string.Empty;
            UprojectPath = string.Empty;
            ProjectName = string.Empty;
            Description = string.Empty;
            Category = string.Empty;
            TagsJson = "[]";
            PresetDataJson = "{}";
            DependenciesJson = "{}";
            Version = "1.0.0";
            IsActive = true;
            IsCompatible = true;
            Hash = string.Empty;
            EngineVersion = string.Empty;
            Author = string.Empty;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            LastSyncedAt = null;
        }
    }
}
