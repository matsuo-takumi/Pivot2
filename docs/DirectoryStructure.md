# 📁 Directory Structure

このドキュメントでは、Pivotプロジェクトの主要なディレクトリ構造とその目的について説明します。

## 1. Complete Directory Tree

```
Pivot/
├── .git/
├── docs/                       # プロジェクトドキュメント (本フォルダ)
│   ├── README.md               # ドキュメントインデックス
│   ├── Architecture.md         # システムアーキテクチャ概要
│   ├── DirectoryStructure.md   # 本ファイル
│   ├── TechStack.md            # 技術スタック
│   ├── DataStructure.md        # データ構造とモデル定義
│   ├── ImplementationTasks.md  # 実装タスクチェックリスト
│   └── Requirement Specification v0.1.md # 要件定義書
├── logs/                       # 履歴および意思決定記録
│   ├── Changelog.md            # 変更ログ
│   ├── DecisionRecords.md      # アーキテクチャ決定記録 (ADR)
│   └── ModificationLog.md      # 詳細な変更記録
├── tips/                       # 開発者向けヒントとナレッジ共有
│   ├── EnvironmentSetup.md     # 開発環境セットアップガイド
│   ├── DebuggingGuide.md       # デバッグガイド
│   └── PerformanceOptimization.md # パフォーマンス最適化のヒント
├── Pivot/                      # WinUI 3アプリケーションのソースコード
│   ├── App.xaml                # アプリケーション定義
│   ├── App.xaml.cs             # アプリケーションのコードビハインド
│   ├── MainWindow.xaml         # メインウィンドウのXAML定義
│   ├── MainWindow.xaml.cs      # メインウィンドウのコードビハインド
│   ├── Assets/                 # アプリケーションアセット (画像、アイコンなど)
│   ├── bin/                    # ビルド済みバイナリ
│   ├── obj/                    # ビルド中間ファイル
│   ├── Properties/             # プロジェクト設定
│   ├── Pivot.csproj            # プロジェクトファイル
│   └── Package.appxmanifest    # MSIXパッケージマニフェスト
└── Pivot.sln                   # Visual Studioソリューションファイル
```

## 2. Explanation of Each Directory

- **`.git/`**: Gitバージョン管理システムに関連するファイルが格納されます。
- **`docs/`**: プロジェクトの主要なドキュメントが格納されます。仕様書、アーキテクチャ、データ構造など。
  - `README.md`: ドキュメント全体のインデックスページ。
  - `Requirement Specification v0.1.md`: アプリケーションの要件定義書。
  - `Architecture.md`: システムの全体アーキテクチャを記述。
  - `DirectoryStructure.md`: 本ファイル。プロジェクトのディレクトリ構成を詳述。
  - `TechStack.md`: 使用されているプログラミング言語、フレームワーク、ツールを記述。
  - `DataStructure.md`: アプリケーションの主要なデータモデルとスキーマを記述。
  - `ImplementationTasks.md`: 実装タスクの進捗を追跡するチェックリスト。
- **`logs/`**: プロジェクトの変更履歴、意思決定、詳細な修正記録が格納されます。
  - `Changelog.md`: 主な変更点のサマリー。
  - `DecisionRecords.md`: アーキテクチャ上の重要な決定とその理由を記録。
  - `ModificationLog.md`: 特定の機能やモジュールに対する詳細な変更履歴。
- **`tips/`**: 開発者向けのヒント、環境設定、デバッグガイドなどのナレッジが格納されます。
  - `EnvironmentSetup.md`: ローカル開発環境のセットアップ手順。
  - `DebuggingGuide.md`: 一般的なデバッグ手法とトラブルシューティングのヒント。
  - `PerformanceOptimization.md`: パフォーマンス最適化のための具体的なアドバイス。
- **`Pivot/`**: WinUI 3アプリケーションのC#ソースコードおよび関連ファイルが格納されるメインプロジェクトディレクトリ。
  - `App.xaml`, `App.xaml.cs`: アプリケーションのエントリポイントとグローバルリソース定義。
  - `MainWindow.xaml`, `MainWindow.xaml.cs`: メインウィンドウのUI定義とロジック。
  - `Assets/`: アプリケーションで使用される画像、アイコン、その他の静的アセット。
  - `bin/`, `obj/`: コンパイルされたバイナリファイルおよびビルドプロセスによって生成される一時ファイル。
  - `Properties/`: プロジェクト固有の設定ファイル（例: `launchSettings.json`）。
  - `Pivot.csproj`: C#プロジェクトの定義ファイル。
  - `Package.appxmanifest`: Windowsアプリケーションパッケージ（MSIX）のマニフェストファイル。アプリケーションのメタデータ、権限、デプロイ情報を含む。
- **`Pivot.sln`**: Visual Studioソリューションファイル。プロジェクトを構成し、IDEで開くためのエントリポイント。

## 3. Folder Naming Conventions and Module Addition Rules

- **PascalCase**: クラス名、プロパティ名、メソッド名、UIコントロール名には `PascalCase` を使用します。
- **camelCase**: プライベートフィールドやローカル変数には `camelCase` を使用します。
- **ファイル名**: 関連する内容を示す明確で簡潔な名前を使用し、`PascalCase` を推奨します。
- **モジュールの追加**: 新しい機能やモジュールを追加する際は、既存のMVVMパターンとDIの原則に従い、`Views/`, `ViewModels/`, `Services/`, `Models/` などの適切なフォルダに配置します。プラグインは `Plugins/` フォルダに配置し、AI関連モジュールは `AI/` フォルダに配置します。
