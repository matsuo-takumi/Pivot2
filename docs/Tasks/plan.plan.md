<!-- 2beea7c3-8c16-4869-955e-ee2a71402c02 26ee024d-1f32-49da-a1a0-a7eadd4a0f87 -->
# Templateタブ（Imageベース）作成プラン

**目的**

- `Image` ページのレイアウトをベースにした新しい `Template` タブを追加し、固定のテストアイテムを表示する。後で `asset` / `image` タブがこのテンプレートを参照する基盤を作る。

**追加/編集するファイル**

- `Pivot/ViewModels/TemplateViewModel.cs`（新規）
- `Pivot/Views/TemplatePage.xaml`（新規）
- `Pivot/Views/TemplatePage.xaml.cs`（新規、コードビハインドでViewModelをDataContextへ紐付け）
- `Pivot/MainWindow.xaml` またはナビゲーション定義ファイル（既存）にテンプレートタブへのエントリを追加

**実装概要**

1. `TemplateViewModel` を作成し、`Image` に近い表示データを再現するために固定のテストデータ（`ImageItem` 互換か簡易モデルのリスト）を公開する。
2. `TemplatePage.xaml` は `ImagePage.xaml` のレイアウトを踏襲し、ItemsControl/ListView を使って `TemplateViewModel` のテストアイテムを表示する。アイテムは全てテスト値にする。
3. `TemplatePage` をアプリのナビゲーションに追加し、メニュー/タブから開けるようにする。
4. 後続作業で `asset` / `image` タブがこの `Template` を参照するためのインターフェース（例: `DataTemplate` を外出し）を確立する準備を残す。

**参考となる簡潔なUIスニペット（提案）**

```xml
<!-- ItemsControl の例。ImagePageの構造を踏襲します -->
<ListView ItemsSource="{Binding TestItems}">
  <ListView.ItemTemplate>
    <DataTemplate>
      <StackPanel Orientation="Vertical">
        <Image Source="{Binding ThumbnailPath}" Width="160" Height="90" />
        <TextBlock Text="{Binding Name}" />
      </StackPanel>
    </DataTemplate>
  </ListView.ItemTemplate>
</ListView>
```

**想定のテストデータ形**

- `TestItems` : ObservableCollection のような一覧。各アイテムは `Name`, `ThumbnailPath`, `Id` などのプロパティを持つ（既存 `ImageItem` を流用推奨）。

**リスク / 注意点**

- 既存のナビゲーション実装（`MainWindow.xaml` など）の場所を特定してから変更する必要がある。ナビ追加箇所が複数ある場合は調整が必要。

**実装Todos（順序依存を含む）**

- template_vm: `TemplateViewModel` を作成し固定テストデータを実装する
- template_view: `TemplatePage.xaml` を作成し `ImagePage` ベースのレイアウトを実装する
- template_codebehind: `TemplatePage.xaml.cs` で ViewModel を DataContext に紐付ける
- add_navigation: `MainWindow.xaml`（またはナビ定義）に `Template` タブ/メニューを追加する

実行して良ければ「実行して」と指示してください。変更はその指示を受けてから行います。