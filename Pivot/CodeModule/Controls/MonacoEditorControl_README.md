# MonacoEditorControl - 使用ガイド

## 概要
`MonacoEditorControl`は、Monaco Editor（VS Codeのエディタエンジン）をWinUI 3アプリケーションで簡単に使用できるようにした再利用可能なコンポーネントです。

## 主な機能
- **シンタックスハイライト**: 40以上の言語をサポート
- **双方向バインディング**: `Text`プロパティでViewModelと自動同期
- **読み取り専用モード**: `IsReadOnly`プロパティで編集を制限
- **自動レイアウト**: ウィンドウサイズに応じて自動調整
- **ミニマップ**: コード全体の概要を表示

## 基本的な使い方

### XAML
```xml
<Page
    xmlns:controls="using:Pivot.CodeModule.Controls">
    
    <controls:MonacoEditorControl 
        Text="{x:Bind ViewModel.Code, Mode=TwoWay}"
        EditorLanguage="csharp"
        Width="600"
        Height="400"/>
</Page>
```

### ViewModel
```csharp
public partial class MyViewModel : ObservableObject
{
    [ObservableProperty]
    private string _code = "// Write your code here";
}
```

## プロパティ

### Text (string)
- エディタの内容
- 双方向バインディング対応
- デフォルト: `string.Empty`

### EditorLanguage (string)
- シンタックスハイライトの言語
- サポート言語: `csharp`, `python`, `javascript`, `typescript`, `cpp`, `java`, `go`, `rust`, `sql`, `html`, `css`, `json`, `xml`, `yaml`, `markdown` など
- デフォルト: `"plaintext"`

### IsReadOnly (bool)
- 読み取り専用モード
- `true`の場合、編集不可
- デフォルト: `false`

### その他のプロパティ
- `CornerRadius`: 角の丸み
- `BorderBrush`: 枠線の色
- `BorderThickness`: 枠線の太さ
- `Background`: 背景色

## 高度な使い方

### ファイルパスから言語を自動判定
```xml
<controls:MonacoEditorControl 
    Text="{x:Bind ViewModel.Code, Mode=TwoWay}"
    EditorLanguage="{x:Bind GetLanguageFromPath(ViewModel.FilePath), Mode=OneWay}"/>
```

```csharp
public string GetLanguageFromPath(string? filePath)
{
    return CodeFileHelper.GetLanguageFromExtension(filePath);
}
```

### プログラムから内容を取得
```csharp
// MonacoEditorControlの参照を取得
var content = await MonacoEditor.GetContentAsync();
```

### 読み取り専用プレビュー
```xml
<controls:MonacoEditorControl 
    Text="{x:Bind ViewModel.PreviewCode}"
    EditorLanguage="python"
    IsReadOnly="True"/>
```

## 実装例

### QuickAddControlでの使用例
```xml
<controls:MonacoEditorControl 
    x:Name="CodeEditor"
    Text="{x:Bind ViewModel.SnippetCode, Mode=TwoWay}"
    EditorLanguage="{x:Bind ViewModel.SelectedLanguage, Mode=OneWay}"
    CornerRadius="8"
    BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
    BorderThickness="1"
    Margin="0,12,0,0"
    Height="300"/>
```

## 注意事項
- Monaco Editorの初期化には数秒かかる場合があります
- WebView2を使用しているため、初回起動時にWebView2 Runtimeが必要です
- `EditorLanguage`プロパティは`FrameworkElement.Language`との衝突を避けるため、`Language`ではなく`EditorLanguage`という名前になっています

## トラブルシューティング

### エディタが表示されない
- `Assets/Monaco/editor.html`が正しく配置されているか確認
- WebView2 Runtimeがインストールされているか確認

### シンタックスハイライトが効かない
- `EditorLanguage`プロパティが正しく設定されているか確認
- サポートされている言語名を使用しているか確認

### 双方向バインディングが動作しない
- `Mode=TwoWay`が設定されているか確認
- ViewModelのプロパティが`ObservableProperty`または`INotifyPropertyChanged`を実装しているか確認
