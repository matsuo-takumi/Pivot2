# ✅ Implementation Tasks

このドキュメントは、Pivotプロジェクトの実装作業をフェーズごとに追跡するためのリビングToDoリストです。
タスクは完了・検証され次第、`[x]`でチェックされます。完了日は記録しません。

## Phase 1: Project Setup and Core Infrastructure
**Dependencies:** None  
**Blockers:** None  
**Related Tickets:** [#1](https://github.com/org/repo/issues/1) (例)

- [x] リポジトリ構造の初期化 (`docs/`, `logs/`, `tips/` フォルダと必須ファイルの作成)  
- [x] WinUI 3プロジェクトの作成と基本構成  
- [x] .NET 8環境のセットアップ  
- [x] MVVMフレームワークの導入 (Community Toolkit MVVMなど)  
- [x] 依存性注入 (DI) コンテナのセットアップ (`Microsoft.Extensions.DependencyInjection`)  
- [x] Serilogによるログ出力の構成  
- [x] SQLite/LiteDBの初期設定と接続確立  
- [x] プロジェクト全体の命名規則とフォルダ構成の確立

---

## Phase 2: Core Data Management
**Dependencies:** Phase 1  
**Blockers:** データモデルの最終確定  
**Related Tickets:** [#5](https://github.com/org/repo/issues/5) (例)

- [x] `MetadataService`の基本実装 (CRUD操作、トランザクション管理)  
- [x] `Files`テーブルのORMモデルとDB操作実装  
- [x] `Images`テーブルのORMモデルとDB操作実装  
- [x] `Projects`テーブルのORMモデルとDB操作実装  
- [x] `Assets`テーブルのORMモデルとDB操作実装  
- [x] `Scripts`テーブルのORMモデルとDB操作実装  
- [x] `UnrealPresets`テーブルのORMモデルとDB操作実装（骨子のみ、後で実装予定）  
- [x] `Preferences`テーブルのORMモデルとDB操作実装  
- [x] `FileScannerService`の実装 (ルートフォルダ指定、再帰スキャン、SQLiteへのメタデータ登録)  
- [x] `FileSystemWatcher`によるファイル変更検知と自動更新の実装  
- [x] スキャン結果のキャッシュ機構 (ハッシュ比較) 実装

---

## Phase 2.5: DB Query API Enhancement
**Dependencies:** Phase 2  
**Blockers:** フィルタ関数の設計確定  
**Related Tickets:** [#7](https://github.com/org/repo/issues/7) (例)

**目的:** Phase 3 の一覧UI（フィルタ・ページング対応）を支える Query API の完成

### フィルタ・ページング API

- [x] `GetFilesByPropertyAsync(skip, take, property?, value?)` - ページング対応ファイル検索
- [x] `GetImagesByPropertyAsync(skip, take, category?, colorSpace?)` - 画像フィルタ+ページング
- [x] `GetAssetsByTypeAsync(type, skip, take)` - アセットタイプ別フィルタ
- [x] `GetAssetsByPropertyAsync(skip, take, type?, category?, size?)` - 複合フィルタ対応
- [x] `GetProjectsByPropertyAsync(skip, take, name?, status?)` - プロジェクト検索
- [x] `GetScriptsByPropertyAsync(skip, take, language?, application?, category?)` - スクリプト複合検索
- [x] `GetUnrealPresetsByPropertyAsync(skip, take, projectName?, category?)` - プリセット検索

### 件数取得 API

- [x] `GetFileCountAsync(property?, value?)` - ファイル総数（オプション条件付き）
- [x] `GetImageCountAsync(category?, colorSpace?)` - 画像総数
- [x] `GetAssetCountAsync(type?, category?)` - アセット総数
- [x] `GetProjectCountAsync(status?)` - プロジェクト総数
- [x] `GetScriptCountAsync(language?, application?)` - スクリプト総数
- [x] `GetUnrealPresetCountAsync(projectName?)` - プリセット総数

### ストリーミング API（大規模フィルタ結果向け）

- [x] `StreamImagesByPropertyAsync(category?, batchSize=500)` - 画像ストリーミング
- [x] `StreamAssetsByPropertyAsync(type?, category?, batchSize=500)` - アセットストリーミング
- [x] `StreamProjectsByPropertyAsync(status?, batchSize=200)` - プロジェクトストリーミング
- [x] `StreamScriptsByPropertyAsync(language?, batchSize=500)` - スクリプトストリーミング

### 複合フィルタ ビルダー（オプション・推奨）

- [ ] `AssetFilterBuilder` - フルエント API でフィルタ条件を組み立て
- [ ] `ImageFilterBuilder` - 同様
- [ ] `ScriptFilterBuilder` - 同様
- [ ] フィルタビルダーで `GetAssetsByFilterAsync(filter, skip, take)` を実装可能に

### インデックス・最適化

- [ ] 各フィルタ関数用に必要なインデックスを `InitializeDatabase()` で追加
  - 例: `Type`, `Category`, `Language`, `Application` など
- [ ] N+1 クエリ回避（Include で関連エントリを事前ロード）
- [ ] フィルタ前後の件数差異に対応（削除済みファイルなど）

---

## Phase 3: UI Development - Core Modules
**Dependencies:** Phase 2  
**Blockers:** Figmaワイヤーフレームの確定  
**Related Tickets:** [#10](https://github.com/org/repo/issues/10) (例)

- [x] `MainViewModel`と`NavigationView`の基本実装 (タブ切り替え機能)  
- [ ] **Assetモジュール**: `AssetViewModel`とAsset一覧表示UIの実装  
- [ ] **Imageモジュール**: `ImageViewModel`とImage表示UIの実装  
- [ ] **Projectモジュール**: `ProjectViewModel`とプロジェクト一覧表示UIの基本実装  
- [x] **Preferenceモジュール**: ディレクトリ設定UIの実装（MVVM準拠、フォルダ選択ダイアログ対応）
- [x] Fluent DesignおよびDark Modeのサポート  
- [ ] Undo-Redo機能のサポート

---

## Phase 3.5: Advanced Project Management UI & Logic
**Dependencies:** Phase 3
**Blockers:** データモデルの依存関係定義の最終確定
**Related Tickets:** [#12](https://github.com/org/repo/issues/12) (例)

- [ ] Projectタブへのバージョン管理連携UIの実装 (Gitステータス表示、コミット履歴など)
- [ ] プロジェクト内の依存関係を視覚化するUIの実装 (グラフ/ツリー表示)
- [ ] 内製タスク管理セクションのUIとデータモデルの実装 (プロジェクトに関連するタスクのCRUD)
- [ ] アセット/スクリプトの「使用箇所」検索機能の実装 (Projectタブ内での検索)
- [ ] プロジェクトデータのアクティビティログ表示機能の実装 (変更履歴など)

---

## Phase 4: AI Classification Integration
**Dependencies:** Phase 2, Phase 3  
**Blockers:** ONNXモデルの準備と評価  
**Related Tickets:** [#15](https://github.com/org/repo/issues/15) (例)

- [ ] ONNX Runtimeの統合  
- [ ] `AIClassifierService`の基本実装 (`AnalyzeAsync`メソッド)  
- [ ] ONNXモデル (CLIP / EfficientNet) のロードと推論実行  
- [ ] `ImageView`へのAI分類パネルの統合 (ボタン、進捗バー)  
- [ ] AI推論結果のタグ付けモード (DBのみ更新) 実装  
- [ ] AI推論結果のフォルダ仕分けモード (実ファイル移動) 実装  
- [ ] 分類カテゴリ定義ファイル (`categories.json`) の読み込みと利用  
- [ ] 推論結果のキャッシュと再利用機構の実装

---

## Phase 5: Advanced Features & External Integration
**Dependencies:** Phase 2, Phase 3, Phase 4  
**Blockers:** 外部DCCツール連携のAPI仕様確定  
**Related Tickets:** [#20](https://github.com/org/repo/issues/20) (例)

- [ ] `UnrealAnalyzer`の実装 (`.uproject`ファイル解析、依存関係抽出)  
- [ ] `ScriptManager`の実装 (スニペット管理、構文ハイライト付きエディタ)  
- [ ] 外部DCCツールへのスクリプト送信機能 (ローカルAPI経由) 実装  
- [ ] `BackupService`の実装 (指定フォルダコピー、ZIP化、バージョン管理、メタDB含む)  
- [ ] `SyncService`の実装 (API連携、OAuth2/トークン認証、クラウド同期)  
- [ ] `PluginManager`の実装 (外部DLL動的ロード、`IPlugin`インターフェース)

---

## Phase 5.5: Project Output & Optimization
**Dependencies:** Phase 5
**Blockers:** 出力フォーマットと変換ロジックの確定
**Related Tickets:** [#22](https://github.com/org/repo/issues/22) (例)

- [ ] カスタム出力プロファイル機能の実装 (ファイル変換、配置設定など)
- [ ] プロジェクトの差分出力機能の実装 (変更点のみを検出し更新)

---

## Phase 6: Testing & QA
**Dependencies:** Phase 5.5
**Blockers:** 全機能の実装完了  
**Related Tickets:** [#25](https://github.com/org/repo/issues/25) (例)

- [ ] 単体テストの実装 (各Service、ViewModel)  
- [ ] 統合テストの実装 (各モジュールの連携)  
- [ ] E2Eテストの計画と一部実装  
- [ ] コードカバレッジツールの導入とレポート生成  
- [ ] 全機能のQA検証チェックリストの実行

---
