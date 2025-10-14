<!-- a4399e8c-91f6-4232-a05b-1f2b96884af4 46b15cad-2c9e-4ff8-9c63-e326ee282f11 -->
# 🧭 Pivot Implementation Roadmap

（推奨フェーズ順）

---

## **Phase 0 – プロジェクト基盤構築**

> 🧱 「アプリを支える骨格」をまず固める

### 🎯 目的

- WinUI 3 + MVVM 環境を整備
- 依存注入・設定管理・ログ機構を確立
- DB（SQLite or LiteDB）の接続を確実に行う

### 🔧 実装内容

1. **WinUI 3 プロジェクトを作成**

   - .NET 8 / Windows App SDK 1.5 以上
   - MVVM Toolkit (`CommunityToolkit.Mvvm`) 導入

2. **DI コンテナ構築**
   ```csharp
   builder.Services.AddSingleton<MetadataService>();
   builder.Services.AddSingleton<FileScannerService>();
   builder.Services.AddSingleton<MainViewModel>();
   ```

3. **設定ファイル管理**

   - `appsettings.json` を用意
   - パス・テーマ・DB接続情報などを格納
   - 起動時に `ConfigurationBuilder` で読み込み

4. **ログ環境整備**

   - `Serilog` を導入
   - `logs/log-.txt` に日次ローテーション出力

5. **SQLite（またはLiteDB）初期化**

   - DB接続確認
   - テーブル自動生成

✅ **成果物:**

アプリ起動 → ナビゲーション付きのシェルウィンドウ → ログが出力される

（この時点で、まだデータもUIも無くてOK）

---

## **Phase 1 – ファイルスキャン & メタデータ基盤**

> 🧭 「現実のフォルダをデータとして理解させる」段階

### 🎯 目的

- 実フォルダ構造を走査し、DBに登録する基本システムを構築
- ハイブリッドモデルの中核

### 🔧 実装内容

1. `FileScannerService`

   - 指定パスを再帰スキャン
   - 画像・動画・PDF・3Dモデルなどを識別
   - 拡張子とMIMEからTypeを判定
   - `FileSystemWatcher`で変更検出

2. `MetadataService`

   - 取得したメタ情報をDB登録
   - ハッシュ（MD5/SHA1）で重複管理
   - 検索・更新・削除API整備

3. **基本UI**

   - 「スキャン開始」ボタン
   - DataGridでスキャン結果一覧を表示

✅ **成果物:**

指定フォルダをスキャン → ファイル一覧がDBに登録・表示される。

これがPivotの中核土台になります。

---

## **Phase 1.5 – 拡張ディレクトリ管理UI**

> 📂 カテゴリ別ディレクトリ管理の基盤を構築し、UIを整備する

### 🎯 目的

- ユーザーがカテゴリ（Asset, Image, Project）ごとにディレクトリを柔軟に管理できるUI基盤を確立する

### 🔧 実装内容

1.  **設定モデルの定義**:

    -   カテゴリ別ディレクトリリストを保持する新しい設定クラス（例: `DirectorySettings`）を定義。
    -   `appsettings.json` (または新規ファイル) にその構造を追加。

2.  **SettingsService の実装**:

    -   カテゴリ別ディレクトリ設定の読み込み、保存、更新を行うサービスを作成。

3.  **PreferencePage の UI 拡張**:

    -   `PreferencePage.xaml` に「Directories」セクションを追加。
    -   Asset/Image/Project ごとにディレクトリリストと追加/削除ボタンを配置。

4.  **DirectoryViewModel の実装**:

    -   `PreferencePage` のUIと `SettingsService` を連携させる ViewModel を作成。
    -   ディレクトリの追加、削除、リスト表示のためのプロパティとコマンドを実装。

5.  **MainViewModel の調整**:

    -   `SettingsService` からディレクトリ設定を読み込み、`FileScannerService` に渡す準備。

6.  **FileScannerService の調整**:

    -   `ScanAsync` メソッドが複数のルートパス（カテゴリ別）を受け取れるように変更。

7.  **AssetViewModel/ImageViewModel の調整**:

    -   `SettingsService` から設定されたディレクトリ情報に基づいて、`MetadataService` から該当するファイルのみを取得・表示するようにフィルタリングロジックを導入。

### ✅ 成果物

- 設定画面でカテゴリ別ディレクトリの追加・削除・表示が可能になる
- `FileScannerService` が複数の指定ディレクトリをスキャンできるようになる
- `AssetPage` および `ImagePage` が設定されたカテゴリディレクトリ内のファイルを自動的にフィルタリングして表示する基盤ができる

---

## **Phase 1.5 – 拡張ディレクトリ管理UI**

> 📂 カテゴリ別ディレクトリ管理の基盤を構築し、UIを整備する

### 🎯 目的

- ユーザーがカテゴリ（Asset, Image, Project）ごとにディレクトリを柔軟に管理できるUI基盤を確立する

### 🔧 実装内容

1.  **設定モデルの定義**:

    -   カテゴリ別ディレクトリリストを保持する新しい設定クラス（例: `DirectorySettings`）を定義。
    -   `appsettings.json` (または新規ファイル) にその構造を追加。

2.  **SettingsService の実装**:

    -   カテゴリ別ディレクトリ設定の読み込み、保存、更新を行うサービスを作成。

3.  **PreferencePage の UI 拡張**:

    -   `PreferencePage.xaml` に「Directories」セクションを追加。
    -   Asset/Image/Project ごとにディレクトリリストと追加/削除ボタンを配置。

4.  **DirectoryViewModel の実装**:

    -   `PreferencePage` のUIと `SettingsService` を連携させる ViewModel を作成。
    -   ディレクトリの追加、削除、リスト表示のためのプロパティとコマンドを実装。

5.  **MainViewModel の調整**:

    -   `SettingsService` からディレクトリ設定を読み込み、`FileScannerService` に渡す準備。

6.  **FileScannerService の調整**:

    -   `ScanAsync` メソッドが複数のルートパス（カテゴリ別）を受け取れるように変更。

7.  **AssetViewModel/ImageViewModel の調整**:

    -   `SettingsService` から設定されたディレクトリ情報に基づいて、`MetadataService` から該当するファイルのみを取得・表示するようにフィルタリングロジックを導入。

### ✅ 成果物

- 設定画面でカテゴリ別ディレクトリの追加・削除・表示が可能になる
- `FileScannerService` が複数の指定ディレクトリをスキャンできるようになる
- `AssetPage` および `ImagePage` が設定されたカテゴリディレクトリ内のファイルを自動的にフィルタリングして表示する基盤ができる

---

## **Phase 2 – Imageタブ & AI分類モジュール（Pivot.AIClassifier）**

> 🤖 「頭脳」を搭載するフェーズ

### 🎯 目的

- AIによる画像分類・タグ付けを実現
- DB連携＋UI統合まで完了させる

### 🔧 実装内容

1. **AI分類モジュール**

   - `ONNX Runtime`導入
   - モデル読み込み → バッチ推論
   - `categories.json`でラベル定義

2. **AIClassifierService**

   - `AnalyzeAsync(IEnumerable<string> paths)`
   - 推論結果を `AITags`, `Confidence` に保存

3. **ImageViewModel**

   - 「AI分類」ボタン → 推論開始 → 進捗表示
   - 分類結果をDataGrid上に反映

✅ **成果物:**

AI分類ボタンを押すと、画像が自動的にタグ分類される。

（ここで初めてPivotの価値を“感じる”段階です）

---

## **Phase 3 – Project / Asset 管理タブ**

> 📦 「整理と依存関係管理」の実装フェーズ

### 🎯 目的

- UEプロジェクト・アセット管理・スクリプト辞書を統合
- UnrealAnalyzer実装の土台づくり

### 🔧 実装内容

1. **ProjectViewModel**

   - プロジェクト一覧・依存ファイル表示
   - UEプリセットを保存・読み込み

2. **UnrealAnalyzer**

   - `.uproject` 解析 → `Dependencies(JSON)`生成

3. **AssetViewModel**

   - プロジェクト単位でのアセットCRUD

✅ **成果物:**

UEプロジェクトを選ぶと必要アセット一覧が表示される。

「依存アセットコピー」警告まで出せる段階。

---

## **Phase 4 – ScriptManager**

> 💻 「コード管理とツール連携」層

### 🎯 目的

- VEX, Pymel, MEL などのスニペット辞書と実行機構
- 将来のHoudini/Mayaとの連携基盤を整える

### 🔧 実装内容

- `ScriptView` + `ScriptViewModel`
- `AvalonEdit` or `RoslynPad` 埋め込み
- コード検索・分類・送信ボタン実装

✅ **成果物:**

Pivot内でスクリプトを検索・編集・DCCに送信できる。

---

## **Phase 5 – ワンクリックバックアップ & Web同期**

> ☁️ 「運用と連携」フェーズ

### 🎯 目的

- 1クリックで全階層バックアップ
- REST API同期によるWIP共有

### 🔧 実装内容

1. `BackupService`

   - 階層構造コピー＋ZIPオプション
   - メタDBも同梱

2. `SyncService`

   - API POST/GET
   - JSONでプロジェクト/アセット送受信
   - 認証（トークン）管理

✅ **成果物:**

「クラウドとローカルの橋渡し」が完成。

---

## **Phase 6 – Plugin System**

> 🧩 「拡張性を確立する」

### 🎯 目的

- 外部DLLをロードしてPivotを拡張可能に
- 内部APIの安定化

### 🔧 実装内容

- `IPlugin`インターフェース設計
- `AssemblyLoadContext`で安全ロード
- メニュー統合・UIイベントバス実装

✅ **成果物:**

他開発者がPivot用プラグインを作れる状態に。

---

## **Phase 7 – Polishing & AI拡張**

> 🧠 「製品レベルへ」到達する最終段階

### 🎯 目的

- UI整備、CLIP検索などの高度化
- キャッシュ最適化、性能チューニング

---

## 🧩 開発優先順位まとめ

| 優先度 | フェーズ      | 目的                     |

| --- | --------- | ---------------------- |

| ⭐⭐⭐ | Phase 0–1 | コア基盤（DB + ファイルスキャン）    |

| ⭐⭐  | Phase 1.5 | 拡張ディレクトリ管理UI         |

| ⭐⭐  | Phase 2   | AI分類機能                 |

| ⭐   | Phase 3–4 | Project・Asset・Script管理 |

| ⚙️  | Phase 5–7 | 同期・拡張・洗練               |

---

## ✅ 結論：最初に実装すべきは

> **Phase 0–1（基盤構築＋フォルダスキャン＋DB管理）**

理由：

- これが「すべてのデータ流通の基点」
- この層が安定しないとAI分類もDB反映も破綻する
- 一度作れば、以降の機能（AI分類、UE解析、Web同期）を**すべて共通のデータパイプライン**に載せられる

---

### To-dos

- [ ] Phase 0 – プロジェクト基盤構築
- [ ] WinUI 3 プロジェクトを作成 (MVVM Toolkit導入)
- [ ] DI コンテナ構築 (サービス登録)
- [ ] 設定ファイル管理 (appsettings.json読み込み)
- [ ] ログ環境整備 (Serilog導入、日次ローテーション出力)
- [ ] SQLite（またはLiteDB）初期化 (DB接続確認、テーブル自動生成)
- [ ] Phase 1 – ファイルスキャン & メタデータ基盤
- [ ] FileScannerService実装 (再帰スキャン、ファイル識別、FileSystemWatcher)
- [ ] MetadataService実装 (メタ情報DB登録、ハッシュ重複管理、検索・更新・削除API)
- [ ] 基本UI実装 (スキャン開始ボタン、DataGridでのスキャン結果一覧表示)
- [ ] Phase 2 – Imageタブ & AI分類モジュール（Pivot.AIClassifier）
- [ ] AI分類モジュール実装 (ONNX Runtime導入、モデル読み込み、バッチ推論、categories.jsonでラベル定義)
- [ ] AIClassifierService実装 (AnalyzeAsyncメソッド、推論結果のAITags/Confidence保存)
- [ ] ImageViewModel実装 (AI分類ボタン、進捗表示、分類結果のDataGrid反映)
- [ ] Phase 3 – Project / Asset 管理タブ
- [ ] ProjectViewModel実装 (プロジェクト一覧・依存ファイル表示、UEプリセット保存・読み込み)
- [ ] UnrealAnalyzer実装 (.uproject解析、Dependencies生成)
- [ ] AssetViewModel実装 (プロジェクト単位でのアセットCRUD)
- [ ] Phase 4 – ScriptManager
- [ ] ScriptView + ScriptViewModel実装
- [ ] AvalonEdit or RoslynPad埋め込み
- [ ] コード検索・分類・送信ボタン実装
- [ ] Phase 5 – ワンクリックバックアップ & Web同期
- [ ] BackupService実装 (階層構造コピー、ZIPオプション、メタDB同梱)
- [ ] SyncService実装 (API POST/GET、JSONでの送受信、認証管理)
- [ ] Phase 6 – Plugin System
- [ ] IPluginインターフェース設計
- [ ] AssemblyLoadContextによる安全ロード
- [ ] メニュー統合・UIイベントバス実装
- [ ] Phase 7 – Polishing & AI拡張
- [ ] UI整備、CLIP検索などの高度化
- [ ] キャッシュ最適化、性能チューニング