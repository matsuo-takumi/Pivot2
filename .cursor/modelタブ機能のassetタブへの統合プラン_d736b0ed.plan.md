---
name: Modelタブ機能のAssetタブへの統合プラン
overview: Assetタブの既存機能（タグ・検索・設定）を維持したまま、Modelタブのようなプレビュー機能付き2カラムレイアウトへ移行し、不要になったModelタブを削除します。
todos:
  - id: extend-asset-viewmodel
    content: AssetViewModel.cs に SelectedAsset プロパティを追加する
    status: pending
  - id: update-asset-ui
    content: AssetPage.xaml を2カラムレイアウトに書き換え、ModelViewerControl を追加する
    status: pending
  - id: link-selection-to-viewer
    content: AssetPage.xaml.cs でクリック時に SelectedAsset を更新する処理を追加する
    status: pending
  - id: cleanup-main-window
    content: MainWindow から Model タブの定義とナビゲーション処理を削除する
    status: pending
  - id: delete-redundant-files
    content: 不要になった Model 関連のファイル（ModelPage, ModelViewModel）を削除する
    status: pending
---

# Modelタブ機能のAssetタブへの統合プラン

このプランでは、`Asset` タブの利便性（Preferenceでの設定やタグ機能）を維持しつつ、`Model` タブが持っていた3Dプレビュー機能を統合します。

## 1. ViewModelの拡張

[`Pivot/ViewModels/AssetViewModel.cs`](Pivot/ViewModels/AssetViewModel.cs) に、プレビューに表示するアセットを保持するための `SelectedAsset` プロパティを追加します。

```csharp
[ObservableProperty]
private TemplateItem? _selectedAsset;
```

## 2. AssetPageレイアウトの変更

[`Pivot/Views/AssetPage.xaml`](Pivot/Views/AssetPage.xaml) を2カラム構成のグリッドに変更します。

- **左カラム (350px程度)**: 既存のタグフィルター (`TagFilterControl`)、レイアウト切替ボタン、アセットリスト (`ItemsRepeater`) を配置。
- **右カラム (*)**: `ModelViewerControl` を配置し、`SelectedAsset.Path` にバインドします。

## 3. 選択連動ロジックの実装

[`Pivot/Views/AssetPage.xaml.cs`](Pivot/Views/AssetPage.xaml.cs) の `AssetItem_PointerPressed` イベントハンドラを更新し、アイテムがクリックされた際に `ViewModel.SelectedAsset` を更新するようにします。これにより、クリックしたアセットが即座に右側でプレビューされます。

## 4. Modelタブの削除とクリーンアップ

統合完了後、重複する機能を削除します。

- **MainWindowからの削除**:
  - [`Pivot/MainWindow.xaml`](Pivot/MainWindow.xaml) から "Model" の `PivotItem` を削除。
  - [`Pivot/MainWindow.xaml.cs`](Pivot/MainWindow.xaml.cs) から `NavigationRegion.Model` への遷移ロジックを削除。
- **定義の削除**:
  - [`Pivot/Models/NavigationRegion.cs`](Pivot/Models/NavigationRegion.cs) から `Model` 列挙型を削除。
- **不要ファイルの物理削除**:
  - [`Pivot/Views/ModelPage.xaml`](Pivot/Views/ModelPage.xaml)
  - [`Pivot/Views/ModelPage.xaml.cs`](Pivot/Views/ModelPage.xaml.cs)
  - [`Pivot/ViewModels/ModelViewModel.cs`](Pivot/ViewModels/ModelViewModel.cs)

## 依存関係図

```mermaid
graph TD
    subgraph AssetModule [Assetタブ (統合先)]
        AVM["AssetViewModel.cs<br/>(SelectedAsset追加)"]
        AX["AssetPage.xaml<br/>(2カラム化)"]
        ACS["AssetPage.xaml.cs<br/>(選択処理追加)"]
    end
    subgraph ModelModule [Modelタブ (削除対象)]
        MV["ModelViewerControl<br/>(再利用)"]
        MP["ModelPage<br/>(削除)"]
        MVM["ModelViewModel<br/>(削除)"]
    end
    subgraph MainContainer [メインウィンドウ]
        MWX["MainWindow.xaml<br/>(タブ削除)"]
        MWC["MainWindow.xaml.cs<br/>(ナビゲーション修正)"]
    end

    AVM --> AX
    ACS --> AVM
    MV --> AX
    AX -.-> MP
    MWX --> MWC
```