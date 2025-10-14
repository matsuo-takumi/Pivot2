# 🧾 Pivot — Requirement Specification v0.1

## 1. Overview

**Pivot** は、アーティスト・テクニカルディレクター・開発者が利用する
「プロジェクト・アセット・リファレンス・スクリプト」を統合管理する **基幹デスクトップソフトウェア** です。

主目的は以下の3点：

1. 各DCCツール（Unreal, Houdini, Maya等）で利用されるアセット群を統一的に管理
2. プロジェクト・リファレンス・スクリプト・テクスチャなどをハブ化
3. AIによる自動分類・タグ付け・整理を通じて、アセット管理の知能化を実現

---

## 2. System Architecture

### 2.1 Platform

| 項目      | 内容                         |
| ------- | -------------------------- |
| 言語      | C# (.NET 8)                |
| フレームワーク | WinUI 3                    |
| アーキテクチャ | MVVM + DI                  |
| データベース  | SQLite または LiteDB          |
| 推論エンジン  | ONNX Runtime               |
| パッケージング | MSIX                       |
| IDE想定   | Visual Studio / Cursor     |
| OS      | Windows 10/11 (x64, ARM64) |

---

### 2.2 Architectural Overview

```
┌──────────────────────────┐
│         Pivot Core        │
│──────────────────────────│
│ UI (WinUI 3)              │
│  ├─ Tabs: Asset / Image / Project / Preference │
│  ├─ AI Classification Panel                    │
│  └─ Plugin Controls (DLL)                      │
│──────────────────────────│
│ Application Layer (MVVM)  │
│  ├─ FileScannerService     │ ← ファイルスキャン・差分検出
│  ├─ MetadataService        │ ← SQLiteメタDB管理
│  ├─ AIClassifierService    │ ← AIタグ・分類機能
│  ├─ UnrealAnalyzer         │ ← UEプリセット依存解析
│  ├─ ScriptManager          │ ← VEX/Pymelスニペット辞書
│  ├─ BackupService          │ ← ワンクリックバックアップ
│  ├─ SyncService (API)      │ ← Web同期
│  └─ PluginManager (DLL Loader) │
│──────────────────────────│
│ Infrastructure Layer      │
│  ├─ SQLite / LiteDB       │
│  ├─ FileSystemWatcher     │
│  ├─ HttpClient (REST)     │
│  └─ ONNX Runtime          │
└──────────────────────────┘
```

---

## 3. Functional Requirements

### 3.1 Core Modules

| モジュール          | 主な機能                               |
| -------------- | ---------------------------------- |
| **Asset**      | 3Dモデル、マテリアル、アニメーション、VFX等のアセットを一元管理 |
| **Image**      | テクスチャ・画像・リファレンス素材を管理。AI分類対応。       |
| **Project**    | プロジェクト単位でアセット関連を整理・依存関係追跡          |
| **Preference** | 設定・テーマ・プラグイン・AIモデル管理               |

---

### 3.2 Detailed Features

#### ① FileScannerService

* ルートフォルダを指定して再帰スキャン
* SQLiteにファイルメタデータ登録
* FileSystemWatcherで変更検知・自動更新
* スキャン結果をキャッシュ（ハッシュ比較）

#### ② MetadataService (Hybrid DB)

* 実フォルダ構造＋DBキャッシュのハイブリッド構成
* メタ情報（タグ・サムネイル・依存関係）を高速参照
* CRUDをトランザクション管理

#### ③ ImageView + AIClassifierService

* ユーザーが複数フォルダを選択 → AI分類開始
* ONNXモデルによる自動カテゴリ検出
* 結果を以下のいずれかで反映：

  * タグ付けモード（DBのみ更新）
  * フォルダ仕分けモード（実ファイル移動）
* 分類カテゴリ定義ファイル（`categories.json`）を参照
* 処理進行率表示・キャンセル対応
* 推論結果をキャッシュして再利用

#### ④ UnrealAnalyzer

* `.uproject`ファイル解析
* Asset依存関係・必要ファイルリストを抽出
* プリセット登録・依存警告機能
* UE用AssetリンクをDBへ登録

#### ⑤ ScriptManager

* VEX, MEL, Pymel, Python, HScriptなどスニペット管理
* 構文ハイライト付きエディタ（AvalonEdit推奨）
* 外部DCCツールへスクリプト送信機能（ローカルAPI経由）
* タグ分類・検索対応

#### ⑥ BackupService

* 指定フォルダ構造を丸ごと階層コピー
* オプションでZIP化
* バージョン別にバックアップ履歴保存
* SQLiteメタDBも含める

#### ⑦ SyncService

* API連携（POST/GET）
* ProjectsやWIP情報をクラウド同期
* フィードバック情報を取得・反映
* OAuth2 / トークン認証対応

#### ⑧ PluginManager

* 外部DLLをプラグインとして動的ロード
* IPluginインターフェースを実装
* メニュー統合・UI拡張・イベントフック可能

---

## 4. Non-Functional Requirements

| 項目                | 内容                                          |
| ----------------- | ------------------------------------------- |
| **パフォーマンス**       | 10,000件の画像を10秒以内でスキャン（マルチスレッド対応）            |
| **信頼性**           | DB破損時の自動リカバリ                                |
| **スケーラビリティ**      | モジュール単位で独立拡張可能                              |
| **可搬性**           | 単体MSIX配布。設定はAppData下にJSON保存                 |
| **セキュリティ**        | API通信をTLS化。ローカルDBは暗号化（AES-256）              |
| **ログ**            | Serilogによる操作・例外ログ出力                         |
| **UI/UX**         | Fluent Design / Dark Mode対応 / Undo-Redoサポート |
| **Accessibility** | ショートカット・スクリーンリーダー対応                         |

---

## 5. Data Model Overview

### 5.1 テーブル構成

| テーブル              | 主なカラム                                                   | 説明         |
| ----------------- | ------------------------------------------------------- | ---------- |
| **Files**         | Id, Path, Type, Size, UpdatedAt, Hash                   | 全ファイルの基礎情報 |
| **Images**        | Id, FileId(FK), Width, Height, AITags(JSON), Confidence | 画像特化情報     |
| **Projects**      | Id, Name, RootPath, Description                         | プロジェクト情報   |
| **Assets**        | Id, ProjectId(FK), Tags, LinkedFiles                    | アセット管理     |
| **Scripts**       | Id, Name, Language, Code, LinkedApp                     | スニペット情報    |
| **UnrealPresets** | Id, UprojectPath, Dependencies(JSON)                    | UEプリセット情報  |
| **Preferences**   | Key, Value                                              | 設定情報       |

---

## 6. State Management (MVVM)

| 層                       | 主なViewModel    | 責務 |
| ----------------------- | -------------- | -- |
| **AssetViewModel**      | アセット一覧・CRUD・検索 |    |
| **ImageViewModel**      | AI分類連携・タグ付け管理  |    |
| **ProjectViewModel**    | プロジェクト単位の階層表示  |    |
| **PreferenceViewModel** | ユーザー設定・テーマ管理   |    |
| **MainViewModel**       | グローバルナビゲーション制御 |    |

---

## 7. UI / UX Design

| 要素                    | 説明                                            |
| --------------------- | --------------------------------------------- |
| **NavigationView**    | 左ペインにタブ（Asset / Image / Project / Preference） |
| **ContentArea**       | タブごとのDataGrid / Canvas / Settings             |
| **AI Panel**          | Imageタブ上部に「AI分類」ボタン＋進捗バー                      |
| **ContextMenu**       | 各アセット右クリックで「AI分類」「タグ付け」「依存表示」                 |
| **Theme**             | Fluent Light/Dark切替、アニメーションTransition         |
| **Canvas（Reference）** | Win2Dベース無限キャンバス、PDF/動画/画像を自由配置                |

---

## 8. AI分類モジュール仕様（Pivot.AIClassifier）

| 項目        | 内容                                                             |
| --------- | -------------------------------------------------------------- |
| **モデル**   | ONNX (CLIP / EfficientNet)                                     |
| **API**   | `IAIClassifierService.AnalyzeAsync(IEnumerable<string> paths)` |
| **出力**    | JSON形式の分類結果（カテゴリ・信頼度）                                          |
| **分類方式**  | 画像→特徴量→カテゴリ類似度算出（Top-3採用）                                      |
| **キャッシュ** | MD5ハッシュ単位でキャッシュ保存                                              |
| **再学習**   | ユーザー手動フィードバックを再学習に利用予定                                         |

---

## 9. Extension and Integration

| 拡張領域                | 説明                          |
| ------------------- | --------------------------- |
| **Plugins**         | 外部DLL（IPlugin実装）を読み込みUI拡張可能 |
| **AI Models**       | ユーザーが独自ONNXモデルを登録可能         |
| **Web API**         | 他アプリとメタデータ連携（JSON Schema対応） |
| **Version Control** | 内部Git構造の導入予定（差分管理・ロールバック）   |

---

## 10. Future Enhancements

1. CLIPによる自然言語検索 ("Search images like 'Cyberpunk sky'")
2. アセット間リンクのグラフ可視化（GraphView）
3. Houdini, Blenderなどへの双方向連携（ローカルAPI）
4. AI自動タグクラスタリング（埋め込み空間分析）
5. Pivot Cloud（チーム共有）

---

## 11. Deliverables (for Development)

* ER図（DB構造）
* クラス図（MVVM構成）
* API Schema（AI分類 / Sync）
* Figmaワイヤーフレーム
* テスト仕様書（機能単位）

---

## 12. 開発ベストプラクティス

| カテゴリ          | 指針                                                                 |
| ------------- | ------------------------------------------------------------------ |
| **命名規則**      | PascalCase (Class), camelCase (fields), Async suffix for async     |
| **フォルダ構成**    | `/Views`, `/ViewModels`, `/Services`, `/Models`, `/Plugins`, `/AI` |
| **依存注入**      | `IServiceCollection` 経由で全Service登録                                 |
| **非同期設計**     | すべてのI/O処理は `async/await` 対応                                        |
| **設定保存**      | JSON形式 (`settings.json`) に保存                                       |
| **ログ設計**      | `Serilog` + RollingFileSink                                        |
| **UIバインディング** | XAML上で `x:Bind` を優先し強い型安全性を確保                                      |
| **プラグイン安全性**  | DLL署名検証・AppDomain隔離ロード                                             |
| **AI推論**      | バッチサイズ調整 + 並列処理制御 + キャッシュ活用                                        |

---

この要件定義は、
次のステップとしてそのまま **Cursor向け仕様書テンプレート**（実装ベース）に変換可能です。

---
