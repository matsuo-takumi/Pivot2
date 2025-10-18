# ✅ Bridge System Implementation Tasks

このドキュメントは、Unreal Engine と Pivot デスクトップアプリケーション間の双方向通信システム（Bridge）の実装作業をフェーズごとに追跡するためのToDoリストです。
将来的に他のDCCツールとの連携も視野に入れています。
タスクは完了・検証され次第、`[x]`でチェックされます。完了日は記録しません。

## Phase 1: Core Communication Infrastructure (Desktop App Side)
**Dependencies:** Pivot Core Data Management (Phase 2)
**Blockers:** なし

- [ ] **通信基盤の設計と実装**
  - [ ] デスクトップアプリ側のHTTPサーバー/WebSocketサーバーの選定とセットアップ (ASP.NET Core Kestrel または類似)
  - [ ] デスクトップアプリ側のAPIエンドポイント定義と実装 (JSONデータ送受信)
    - [ ] `/unreal/send-assets` (POST): Unreal -> Desktop (アセット情報)
    - [ ] `/desktop/assets` (GET): Desktop -> Unreal (保存データ取得)
    - [ ] `/desktop/send-to-unreal` (POST): Desktop -> Unreal (アセット/プリセット送信)
  - [ ] JSONシリアライザー/デシリアライザーの統合 (`System.Text.Json`)
  - [ ] 通信結果のログ記録 (Serilogとの連携)

- [ ] **データモデルの定義**
  - [ ] `UnrealAssetEntry` モデルの定義 (Unrealからのアセット情報受信用)
  - [ ] `UnrealPresetEntry` モデルの定義 (Unrealからのシーンプリセット情報受信用)
  - [ ] これらのモデルと既存の `AssetEntry`, `ProjectEntry` との連携方法の検討

- [ ] **サービス層の実装**
  - [ ] `BridgeService` の基本実装 (通信処理、データ変換、MetadataServiceとの連携)
  - [ ] 受信したUnrealアセット情報を `MetadataService` を介してDBに保存するロジック

- [ ] **UI/UXの基本実装**
  - [ ] メイン画面にBridgeの接続ステータス表示 (例: 接続中/未接続)
  - [ ] Unrealとの同期ボタン (例: 「Unrealと同期」ボタン)
  - [ ] 通信結果をユーザーに通知する仕組み (トースト通知など)
  - [ ] 非同期通信 (`async/await`) のUIスレッドブロック回避

## Phase 2: Unreal Engine Plugin Development
**Dependencies:** Phase 1 (Core Communication Infrastructure)
**Blockers:** Unreal Engine開発環境のセットアップ

- [ ] **Unreal Engineプラグインプロジェクトの作成**
  - [ ] `.uplugin` ファイルの作成と基本設定
  - [ ] C++ モジュールの初期設定

- [ ] **通信クライアントの実装**
  - [ ] デスクトップアプリへのHTTPリクエスト送信ロジック (`FHttpModule`)
  - [ ] WebSocketクライアントの実装 (`IWebSocket`)

- [ ] **アセット情報抽出の実装**
  - [ ] Unrealアセットのメタデータ（パス、タイプ、参照など）を抽出するロジック (`UAssetSyncModule`)
  - [ ] 抽出したアセット情報をJSON形式に変換するロジック

- [ ] **シーンプリセット抽出・適用ロジックの実装**
  - [ ] Unrealシーンの特定プロパティや設定をプリセットとして抽出するロジック
  - [ ] 受信したプリセットをUnrealシーンに適用するロジック (`UPresetManager`)

## Phase 3: Bidirectional Synchronization and Advanced Features
**Dependencies:** Phase 1, Phase 2
**Blockers:** Unreal側の実装詳細

- [ ] **プリセットの双方向同期**
  - [ ] Unrealでエクスポートされたプリセットをアプリで編集し、Unrealに再インポートするワークフローの確立
  - [ ] JSONスキーマの共通化によるデータ整合性維持

- [ ] **リアルタイム同期 (WebSocket)**
  - [ ] デスクトップアプリとUnrealプラグイン間のWebSocket接続確立
  - [ ] リアルタイムでのアセット/プリセットの状態更新ロジック

- [ ] **UX改善**
  - [ ] 同期中のアニメーション表示
  - [ ] 接続ステータスの詳細表示

- [ ] **エラーハンドリングと堅牢性**
  - [ ] 通信エラー時の自動リトライメカニズム
  - [ ] 接続切断時の再接続ロジック
  - [ ] 詳細なログ記録と診断情報

## Phase 4: Integration with Pivot Core & UI Development
**Dependencies:** Phase 3, Pivot Phase 2 & 3
**Blockers:** なし

- [ ] **Pivotの既存機能との連携**
  - [ ] Bridgeで管理するアセット/プリセットとPivotの `AssetEntry` / `ProjectEntry` / `FileEntry` との連携
  - [ ] Unrealから受信した情報をPivotのUIで表示するためのViewModel/Viewの実装

- [ ] **UIモジュールの実装**
  - [ ] Bridge専用の `ViewModel` (`BridgeViewModel`) の作成
  - [ ] Bridgeのステータス、ログ、同期状況を表示する専用UI (`BridgePage.xaml`)
  - [ ] アセット/プリセットの一覧表示と操作UI

## Phase 5: Future Enhancements & Other DCC Tools
**Dependencies:** Phase 4
**Blockers:** 必要に応じて

- [ ] アセット差分表示機能の実装
- [ ] Unreal シーンのリアルタイム更新ビュー
- [ ] gRPCなど他プロトコルへの拡張
- [ ] Maya/Blenderなど他のDCCツールとの連携を考慮した汎用化
