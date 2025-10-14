# 🏛️ Architecture Overview

## 1. System Overview and Purpose

**Pivot** は、アーティスト・テクニカルディレクター・開発者が利用する
「プロジェクト・アセット・リファレンス・スクリプト」を統合管理する **基幹デスクトップソフトウェア** です。

詳細な目的は要件定義書を参照してください。

## 2. Architecture Diagram

```mermaid
graph TD
    User[ユーザー] --> UI(UI層: WinUI 3)
    UI --> AppLayer(アプリケーション層: MVVM)
    AppLayer --> InfraLayer(インフラストラクチャ層)
    
    subgraph UI層
        UI -- タブ --> AssetView(Asset View)
        UI -- タブ --> ImageView(Image View)
        UI -- タブ --> ProjectView(Project View)
        UI -- タブ --> PreferenceView(Preference View)
        UI -- パネル --> AIClassificationPanel(AI Classification Panel)
        UI -- 拡張 --> PluginControls(Plugin Controls)
    end
    
    subgraph アプリケーション層 (MVVM)
        AppLayer -- ファイル操作 --> FileScannerService
        AppLayer -- メタデータ管理 --> MetadataService
        AppLayer -- AI分類 --> AIClassifierService
        AppLayer -- Unreal依存解析 --> UnrealAnalyzer
        AppLayer -- スクリプト管理 --> ScriptManager
        AppLayer -- バックアップ --> BackupService
        AppLayer -- Web同期 --> SyncService(API)
        AppLayer -- プラグインロード --> PluginManager(DLL Loader)
    end
    
    subgraph インフラストラクチャ層
        InfraLayer -- データ永続化 --> SQLite_LiteDB(SQLite / LiteDB)
        InfraLayer -- ファイル変更監視 --> FileSystemWatcher
        InfraLayer -- Web通信 --> HttpClient(REST)
        InfraLayer -- AI推論 --> ONNXRuntime(ONNX Runtime)
    end
    
    FileScannerService --> SQLite_LiteDB
    MetadataService --> SQLite_LiteDB
    AIClassifierService --> ONNXRuntime
    SyncService --> HttpClient
    PluginManager --> PluginControls
```

## 3. Major Components and Their Roles

- **UI (View - WinUI 3)**: ユーザーインターフェースを提供します。アセット表示、画像ビューア、プロジェクト管理、設定画面などが含まれます。ViewはViewModelからデータを取得し、ユーザー操作をViewModelに伝えます。
- **Application Layer (ViewModel)**: ViewとModelの間の仲介役として機能し、UIに表示するデータを準備し、Viewからのコマンドを処理します。データ処理、AI分類、DCCツール連携、バックアップ、同期などのビジネスロジックへの橋渡しを担当します。
- **Infrastructure Layer (Model / Service)**: ビジネスロジック（Model）の実装とデータアクセス（Service）を担当します。データの永続化（SQLite/LiteDB）、ファイルシステムの監視、ネットワーク通信、AI推論エンジン（ONNX Runtime）などの基盤機能を提供します。

## 4. Communication Flow Between Modules

- **UI と Application Layer**: MVVMパターンにより、ViewModelがViewにデータを提供し、Viewからのユーザー操作をViewModelが処理します。データバインディングとコマンドにより疎結合を実現します。
- **Application Layer と Infrastructure Layer**: DI（依存性注入）により、各サービスが具体的なインフラスト実装に依存せず、インターフェースを介して連携します。
- **FileScannerService**: 指定されたルートフォルダを監視し、ファイルの追加・変更・削除を検知。メタデータサービスを通じてDBに反映します。
- **AIClassifierService**: ユーザーが選択した画像やアセットに対してONNX Runtimeを用いてAI分類を実行し、結果をメタデータサービスに渡します。
- **SyncService**: HttpClientを介して外部APIと通信し、クラウドとのデータ同期を行います。

## 5. Chosen Architecture Pattern

- **MVVM (Model-View-ViewModel)**: UI層とビジネスロジック層の明確な分離を目的として採用。テスト容易性、保守性、拡張性を高めます。
- **DI (Dependency Injection)**: 各コンポーネント間の依存関係を疎結合に保ち、モジュールごとの独立性とテストの容易性を向上させます。

## 6. Scalability, Performance, and Fault Tolerance Design Strategies

- **Scalability**:
  - モジュール単位での独立した設計により、機能拡張が容易です。
  - プラグインアーキテクチャにより、外部機能の追加・削除が容易です。
- **Performance**:
  - `FileScannerService`はマルチスレッドでのファイルスキャンに対応し、大量のファイルを高速に処理します。
  - `MetadataService`は実フォルダ構造とDBキャッシュのハイブリッド構成により、メタデータの高速参照を実現します。
  - `AIClassifierService`は推論結果をキャッシュし、再利用することで処理速度を向上させます。
- **Fault Tolerance**:
  - `MetadataService`はCRUD操作をトランザクション管理し、DB破損時の自動リカバリ機能を実装します。
  - `Serilog`による詳細なログ出力により、問題発生時の原因特定と復旧を支援します。
