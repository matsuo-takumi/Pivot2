# 🧩 Environment Setup

このドキュメントでは、Pivotプロジェクトの開発環境をセットアップするための手順を説明します。

## 1. Prerequisites

- **OS**: Windows 10/11 (x64)
- **Visual Studio**: 2022以降 (C# .NET デスクトップ開発ワークロードを含む)
- **.NET SDK**: .NET 8.0 SDK
- **Git**: 最新バージョン
- **Optional**: Docker Desktop (データベースのローカル実行を検討する場合)

## 2. Setup Steps

1. **リポジトリをクローンする**
   ```bash
   git clone https://github.com/your-org/Pivot.git
   cd Pivot
   ```

2. **Visual Studio でソリューションを開く**
   - `Pivot.sln` を Visual Studio で開きます。

3. **NuGetパッケージの復元**
   - ソリューションを開くと、Visual Studioが自動的に必要なNuGetパッケージを復元します。
   - もし復元されない場合は、ソリューションエクスプローラーでソリューションを右クリックし、「NuGetパッケージの復元」を選択します。

4. **依存性注入の設定**
   - `App.xaml.cs` (または類似のアプリケーションエントリポイント) にて、`IServiceCollection` を使用して各種サービスが登録されていることを確認します。
   - 必要なサービス（例: `FileScannerService`, `MetadataService`, `AIClassifierService` など）をDIコンテナに登録します。

5. **データベースの初期化**
   - ローカルデータベース (SQLite/LiteDB) のファイルは、アプリケーションの初回起動時に自動的に作成されるように設計されています。
   - 開発中にデータベーススキーマを変更した場合は、マイグレーションスクリプトを実行するか、データベースファイルを削除して再構築する必要がある場合があります。

6. **AIモデルの配置**
   - ONNXモデルファイル (例: `clip_model.onnx`, `efficientnet_model.onnx`) は、プロジェクト内の指定されたディレクトリ (`/AI/Models` など) に配置する必要があります。
   - これらのモデルは、アプリケーションのビルド時に出力ディレクトリにコピーされるようにプロジェクト設定 (`.csproj`) を確認してください。

7. **開発ビルドと実行**
   - Visual StudioでF5キーを押すか、「デバッグ」->「デバッグ開始」を選択してアプリケーションをビルドし、実行します。
   - アプリケーションが正常に起動し、UIが表示されることを確認します。

## 3. Configuration Variables

- **`settings.json`**: アプリケーションの設定はJSON形式のファイル (`settings.json`) に保存されます。このファイルは、アプリケーションの実行可能ファイルと同じディレクトリ、または `AppData` フォルダに配置されます。
  - **AIモデルパス**: 利用するONNXモデルのファイルパス。
  - **ルートフォルダ**: `FileScannerService` がスキャンするデフォルトのルートフォルダ。
  - **APIキー**: `SyncService` がクラウドAPIと連携するためのAPIキーやトークン (開発環境用)。

## 4. Debugging Tips

- `DebuggingGuide.md` を参照してください。

## 5. Troubleshooting

- `Serilog`によるログ (`logs/`) を確認し、エラーや警告メッセージがないか調べます。
- Visual Studioの出力ウィンドウで、デバッグメッセージや例外を確認します。
- `ImplementationTasks.md` を参照し、現在の開発フェーズにおける既知の問題やブロックを確認します。
