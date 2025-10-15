# ✅ Implementation Tasks

このドキュメントは、Pivotプロジェクトの実装作業をフェーズごとに追跡するためのリビングToDoリストです。
タスクは完了・検証され次第、`[x]`でチェックされます。完了日は記録しません。

## Phase 1: Project Setup and Core Infrastructure
**Dependencies:** None  
**Blockers:** None  
**Related Tickets:** [#1](https://github.com/org/repo/issues/1) (例)

- [ ] リポジトリ構造の初期化 (`docs/`, `logs/`, `tips/` フォルダと必須ファイルの作成)  
- [ ] WinUI 3プロジェクトの作成と基本構成  
- [ ] .NET 8環境のセットアップ  
- [ ] MVVMフレームワークの導入 (Community Toolkit MVVMなど)  
- [ ] 依存性注入 (DI) コンテナのセットアップ (`Microsoft.Extensions.DependencyInjection`)  
- [ ] Serilogによるログ出力の構成  
- [ ] SQLite/LiteDBの初期設定と接続確立  
- [ ] プロジェクト全体の命名規則とフォルダ構成の確立

---

## Phase 2: Core Data Management
**Dependencies:** Phase 1  
**Blockers:** データモデルの最終確定  
**Related Tickets:** [#5](https://github.com/org/repo/issues/5) (例)

- [ ] `MetadataService`の基本実装 (CRUD操作、トランザクション管理)  
- [ ] `Files`テーブルのORMモデルとDB操作実装  
- [ ] `Images`テーブルのORMモデルとDB操作実装  
- [ ] `Projects`テーブルのORMモデルとDB操作実装  
- [ ] `Assets`テーブルのORMモデルとDB操作実装  
- [ ] `Scripts`テーブルのORMモデルとDB操作実装  
- [ ] `UnrealPresets`テーブルのORMモデルとDB操作実装  
- [ ] `Preferences`テーブルのORMモデルとDB操作実装  
- [ ] `FileScannerService`の実装 (ルートフォルダ指定、再帰スキャン、SQLiteへのメタデータ登録)  
- [ ] `FileSystemWatcher`によるファイル変更検知と自動更新の実装  
- [ ] スキャン結果のキャッシュ機構 (ハッシュ比較) 実装

---

## Phase 3: UI Development - Core Modules
**Dependencies:** Phase 2  
**Blockers:** Figmaワイヤーフレームの確定  
**Related Tickets:** [#10](https://github.com/org/repo/issues/10) (例)

- [ ] `MainViewModel`と`NavigationView`の基本実装 (タブ切り替え機能)  
- [ ] **Assetモジュール**: `AssetViewModel`とAsset一覧表示UIの実装  
- [ ] **Imageモジュール**: `ImageViewModel`とImage表示UIの実装  
- [ ] **Projectモジュール**: `ProjectViewModel`とプロジェクト一覧表示UIの基本実装  
- [ ] **Preferenceモジュール**: `PreferenceViewModel`と設定UIの基本実装  
- [ ] Fluent DesignおよびDark Modeのサポート  
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
