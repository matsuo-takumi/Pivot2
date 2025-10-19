<!-- b1c32c26-712b-4723-8f25-26e142301190 3e335913-3ee6-4375-8cc9-419c560e2167 -->
# Asset: 3D ビューワ実装（ContentIsland + Direct3D 11 + Assimp）

## 目的

- Asset グリッドのカードをダブルクリックで、3D ビューワウィンドウを起動。
- Direct3D 11（ContentIsland 経由）でメッシュ・テクスチャを高速レンダリング。
- Assimp で FBX/GLTF/OBJ などの 3D モデルを読み込み。
- マウス操作でカメラ回転・パン・ズーム、基本的なマテリアル・ライト表示。

## 実装フェーズ（段階的進行）

### フェーズ 1: ContentIsland + Direct3D 11 基盤

シンプルな立方体を ContentIsland 内で Direct3D 11 で描画する。

主な作業:

- Windows App SDK 依存の確認（ContentIsland 利用可能か検証）
- 新規ファイル: `Pivot/Render/Direct3D11Renderer.cs`（D3D11 デバイス・SwapChain セットアップ、メッシュ描画）
- 新規ファイル: `Pivot/Views/Asset3DViewerWindow.xaml` / `.xaml.cs`（ContentIsland ホスト）
- ContentIsland のセットアップ、レンダスレッド分離の基本実装
- 出力: 回転する立方体が表示される

### フェーズ 2: Assimp 統合 + モデル読み込み

FBX/GLTF/OBJ ファイルを読み込み、Direct3D メッシュに変換して表示。

主な作業:

- NuGet で Assimp パッケージ追加（C# ラッパー版）
- 新規ファイル: `Pivot/Render/Model3DLoader.cs`（Assimp から Mesh/Material 抽出）
- メモリ管理・テクスチャロード処理
- ビューワウィンドウにモデルパス（AssetEntry）を渡すロジック
- 出力: ユーザーが選択した 3D モデルがビューワで表示される

### フェーズ 3: Asset グリッド統合 + ダブルクリックイベント

Asset ページのグリッドカードでダブルクリックを検知し、ビューワを起動。

主な作業:

- `Pivot/ViewModels/AssetViewModel.cs` に `LaunchViewer3DCommand` 追加
- `Pivot/Views/AssetPage.xaml.cs` でダブルクリックハンドラ追加
- Asset グリッドカード DataTemplate に `DoubleTapped` イベント追加
- ビューワウィンドウ起動ロジック
- 出力: グリッド内のカードをダブルクリック → 3D ビューワ起動

### フェーズ 4: インタラクション実装（カメラ制御・マテリアル表示）

マウス/キーボード操作でカメラを制御、マテリアル/ライト設定 UI を追加。

主な作業:

- `Pivot/Render/Camera3D.cs`（カメラクラス：回転/パン/ズーム）を作成
- Mouse 入力（MouseDown/MouseMove/MouseWheel）ハンドラ追加
- 右サイドパネルに「マテリアル切替」「ライト調整」スライダーを追加
- マテリアル選択時の D3D11 シェーダ状態変更ロジック
- 出力: カメラ回転・ズーム可能、マテリアルプレビュー・ライト調整が可能

## 変更対象ファイル（新規 + 既存）

### 新規作成

- `Pivot/Render/Direct3D11Renderer.cs` — D3D11 初期化、SwapChain、メッシュ描画、レンダループ
- `Pivot/Render/Model3DLoader.cs` — Assimp 連携、Mesh/Material 抽出
- `Pivot/Render/Camera3D.cs` — カメラクラス（行列計算、入力処理）
- `Pivot/Views/Asset3DViewerWindow.xaml` — ビューワウィンドウの XAML（ContentIsland + パネル）
- `Pivot/Views/Asset3DViewerWindow.xaml.cs` — ビューワコードビハインド（ウィンドウライフサイクル、イベント）

### 既存ファイル修正

- `Pivot/ViewModels/AssetViewModel.cs` — `LaunchViewer3DCommand` + 関連プロパティ追加
- `Pivot/Views/AssetPage.xaml` — グリッドカード DataTemplate に `DoubleTapped` イベント追加
- `Pivot/Views/AssetPage.xaml.cs` — ダブルクリックハンドラ実装、ViewModel コマンド呼び出し
- `Pivot.csproj` — NuGet 参照に Assimp.NET (C#) を追加

## 技術スタック・依存性

| 層 | 使用技術 | 備考 |
|-----|---------|------|
| UI ホスト | ContentIsland (Windows App SDK 1.6+) | WinUI3 と統合 |
| レンダリング | Direct3D 11 (WinRT) | 標準 Windows グラフィックス |
| 3D 読み込み | Assimp.NET (NuGet) | FBX/GLTF/OBJ 対応 |
| 数学 | System.Numerics (Matrix4x4 等) | .NET 標準 |
| スレッド同期 | DispatcherQueue | メイン ↔ レンダスレッド 通信 |

## 実装上の注意点

1. **ContentIsland のスレッド安全性**

- ContentIsland はメインスレッド（UI）から初期化し、レンダはワーカースレッドで実行。
- DispatcherQueue を使い、スレッド間のデータ同期を行う。

2. **メモリ管理**

- Direct3D リソース（Texture, VertexBuffer, etc）は COM オブジェクト → `IDisposable` で厳密に管理。
- 大規模メッシュは LOD/リダクション検討。

3. **ビルド・デプロイ**

- Assimp.NET の native DLL は AppX パッケージに含める必要あり（x64/ARM64 対応）。
- 開発時は `bin\x64\Debug` に DLL が自動配置されるか確認。

4. **パフォーマンス**

- ContentIsland レンダスレッド内で 60 FPS を目指す（フレームスキップ回避）。
- テクスチャ圧縮（BC1/BC7）を検討し、GPU メモリ使用を削減。

## リスク・回避策

| リスク | 回避策 |
|--------|--------|
| Assimp DLL ビルド失敗 | NuGet プリコンパイル版を使用、手動で AppX に含める |
| ContentIsland の互換性 | Windows App SDK 1.6+ 要件確認、特定バージョン固定 |
| 大規模メッシュのメモリ | サンプルは中サイズ（<10M 頂点）で検証、LOD 検討 |
| レンダスレッド同期問題 | DispatcherQueue で厳密に同期、デッドロック検証 |

## 次のステップ

1. 本プランを確認・承認いただく。
2. フェーズ 1 から実装開始。
3. 各フェーズの完了後、テストと次フェーズへ。

### To-dos

- [x] AssetViewModel にデータロード、ページング、フィルタロジックを実装
- [x] AssetPage.xaml に ItemsRepeater + フィルタ UI を実装
- [x] AssetPage.xaml.cs でスクロールイベント購読し無限スクロール制御
- [ ] Asset タブの統合テスト（ディレクトリ登録 → リスト表示 → スクロール → フィルタ）