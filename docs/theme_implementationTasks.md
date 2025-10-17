# Themeの実装タスク

## 概要
PreferencePage内のTheme設定画面において、アプリケーションの視覚スタイルとテーマを管理するUIと機能を実装します。

## 主要な実装項目

1.  **Mica Styleの実装**
    *   `MainWindow.xaml`または`App.xaml`にてMicaBackdropを適用する。
    *   設定画面でMicaスタイルを選択・適用できる機能を提供する。

2.  **AcrylicThin Styleの実装**
    *   AcrylicBrushを適切に適用し、背景の半透明効果を実装する。
    *   設定画面でAcrylicThinスタイルを選択・適用できる機能を提供する。
    *   **AcrylicBrushのプロパティ（Tint Opacity, Tint Color, Fallback Color）をアプリ内で制御できるようにする。**

3.  **Mica Alt Styleの実装**
    *   MicaBackdropのAltバージョンを適用する。
    *   設定画面でMica Altスタイルを選択・適用できる機能を提供する。
    *   **Mica Altのプロパティ（MicaKind.BaseAltの調整など）をアプリ内で制御できるようにする。**

4.  **ライトテーマとダークテーマの実装**
    *   `App.xaml`のリソースディクショナリを更新し、`RequestedTheme`プロパティを切り替えることでライト/ダークテーマを適用できるようにする。
    *   ユーザーが設定画面でライト/ダークテーマを選択できるUIを提供する。

5.  **(オプション) 完全任意のウィンドウカラーの変更機能**
    *   カラーピッカーなどのUI要素を導入し、ユーザーが自由にアプリケーションの基調色を選択できる機能を提供する。
    *   選択された色をアプリケーション全体に反映させるロジックを実装する。
    *   **Luminosityに関するプロパティ（Tint Opacity, Tint Luminosity Opacity, Tint Color, Fallback Color）をアプリ内で制御できるようにする。**

## 実装詳細

*   `ThemePage.xaml`と`ThemePage.xaml.cs`を更新し、上記機能のUI要素（ラジオボタン、スイッチ、カラーピッカーなど）を配置する。
*   テーマ設定を保存・読み込みするためのロジックを`SettingsService.cs`または新しいサービスとして実装する。
    *   **新しい設定プロパティ（Acrylic/Mica Alt/Luminosity関連のOpacity, Colorなど）を`UserSettings.cs`に追加する。**
    *   **`SettingsService.cs`にこれらの新しい設定プロパティを保存・読み込みするメソッドを追加する。**
*   `MainViewModel`または`ThemeViewModel`（必要であれば新規作成）を更新し、UIとロジック間のデータバインディングを確立する。
    *   **`ThemeViewModel`に新しい設定プロパティと、それらを変更するためのコマンド/プロパティを追加する。**
    *   **`ThemePage.xaml.cs`で、これらのViewModelプロパティをUI要素にバインドする。**
*   各スタイル（Mica, AcrylicThin, Mica Alt）の適用は、`MainWindow.xaml.cs`の`MainWindow`コンストラクタ内で、選択された設定に基づいて動的に切り替える必要がある。

## 考慮事項

*   アクセシビリティ（高コントラストテーマなど）への対応。
*   パフォーマンスへの影響（特にAcrylicBrush）。
*   設定の永続化と、アプリケーション起動時のテーマ適用。
