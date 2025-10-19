# ✅ スキャン結果のキャッシュ機構実装ガイド

## 目的

ファイルシステムスキャンは、大規模なディレクトリツリーをトラバースし、全ファイルのハッシュを計算する処理です。このため、スキャン時間が非常に長くなる可能性があります。

**スキャン結果のキャッシュ機構**により、前回のスキャン結果と比較して変更されたファイルのみを再処理することで、スキャン時間を大幅に削減できます。

---

## 設計概要

### 1. キャッシュデータ構造（`ScanCacheEntry`）

```csharp
public class ScanCacheEntry
{
    [BsonId]
    public string FilePath { get; set; }        // ユニークキー：ファイルの絶対パス
    public string Hash { get; set; }            // ファイルの xxHash64 ハッシュ値
    public long Size { get; set; }              // ファイルサイズ（バイト）
    public DateTime LastModifiedUtc { get; set; } // ファイルの最終更新時刻（UTC）
    public DateTime CachedAt { get; set; }      // キャッシュ作成・更新時刻
    public string RootPath { get; set; }        // スキャン対象ルートパス
}
```

**設計ポイント**:
- `FilePath` が `[BsonId]` なため、LiteDB は自動的にユニークインデックスを作成し、ファイルパスによる高速な検索が可能
- `Size` と `LastModifiedUtc` の組み合わせで、ファイルの内容変更を検知（ハッシュ計算前の軽量チェック）
- `RootPath` により、複数のスキャンルートを区別し、特定ルートのキャッシュのみをクリア可能

### 2. キャッシュ操作API（`MetadataService`）

```csharp
// キャッシュ追加/更新
await _metadataService.UpsertScanCacheAsync(new ScanCacheEntry(...));

// 単一ファイルのキャッシュ取得
ScanCacheEntry? cache = await _metadataService.GetScanCacheAsync(filePath);

// ルート単位でのキャッシュ取得・削除
List<ScanCacheEntry> caches = await _metadataService.GetScanCacheByRootAsync(rootPath);
await _metadataService.ClearScanCacheByRootAsync(rootPath);

// 全キャッシュ削除
await _metadataService.ClearAllScanCacheAsync();
```

### 3. キャッシュを活用した高速スキャン（`FileScannerService`）

`ScanWithCacheAsync` メソッドは、2段階のスキャンプロセスを実装しています：

#### 第1段階：キャッシュに基づいた変更検知
```
全ファイル → [HasFileChangedAsync] → 変更されたファイルのみを抽出
  （サイズ・最終更新時刻で高速チェック）
```

- ファイルシステムを列挙
- 各ファイルについて、キャッシュの `Size` と `LastModifiedUtc` と現在値を比較
- 変更があったファイルのみ第2段階に進む

#### 第2段階：変更ファイルの処理
```
変更ファイル → [ハッシュ計算] → [DB更新] → [キャッシュ更新]
```

- ハッシュ値を計算
- メタデータDB（`FileEntry`, `AssetEntry` など）に保存
- スキャンキャッシュを最新化

---

## 実装の詳細

### ファイル変更検知ロジック（`HasFileChangedAsync`）

```csharp
private async Task<bool> HasFileChangedAsync(string path, CancellationToken cancellationToken)
{
    var fileInfo = new FileInfo(path);
    var cacheEntry = await _metadataService.GetScanCacheAsync(path);

    if (cacheEntry == null)
        return true;  // キャッシュなし = 新規ファイル

    // サイズと最終更新時刻が両方同じなら、変更なし
    if (cacheEntry.Size == fileInfo.Length && 
        cacheEntry.LastModifiedUtc == fileInfo.LastWriteTimeUtc)
        return false;  // 変更なし

    return true;  // 変更あり
}
```

**ポイント**:
- ハッシュ計算は**実行しない**（これが高速化の鍵）
- `Size` と `LastModifiedUtc` の両方が同じ = ファイル内容は変わっていないと仮定
- 稀に偽陽性（変更なしを変更ありと判定）が発生する可能性があるため、エラー時は安全側（変更あり）と判定

### スキャン進捗レポート

`ScanWithCacheAsync` は、スキャンの2つの段階をプログレスバーに反映：

```
0% ─────────────────── 50% ─────────────────── 100%
  キャッシュ確認段階      ファイル処理段階
  (全ファイル)            (変更ファイルのみ)
```

---

## パフォーマンス効果

### シナリオ別の効果

| シナリオ | スキャン対象 | 期待効果 |
|--------|-----------|--------|
| **初回スキャン** | 全ファイル | 効果なし（キャッシュ生成） |
| **変更小** | ≈5%のファイルが変更 | ~95%高速化（ハッシュ計算削減） |
| **変更中** | ≈50%のファイルが変更 | ~50%高速化 |
| **変更大** | ≈99%のファイルが変更 | ほぼ効果なし（全ファイル処理） |

### 実装例

```csharp
// キャッシュ機構を使用した高速スキャン
var progress = new Progress<int>(p => 
    DispatcherQueue.TryEnqueue(() => ProgressBar.Value = p)
);

await _fileScannerService.ScanWithCacheAsync(
    new[] { "D:\\Assets" },
    progress,
    cancellationToken
);

// 従来の全スキャン（キャッシュなし）
// await _fileScannerService.ScanAsync(
//     new[] { "D:\\Assets" },
//     progress,
//     cancellationToken
// );
```

---

## キャッシュ管理のベストプラクティス

### 1. キャッシュの自動クリーンアップ

古いキャッシュエントリは定期的に削除すべきです（例：30日以上未アクセス）：

```csharp
public async Task CleanupStaleCache(int retentionDays = 30)
{
    var allCache = await _metadataService.GetAllScanCacheAsync();
    var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
    
    foreach (var entry in allCache.Where(c => c.CachedAt < cutoff))
    {
        await _metadataService.DeleteScanCacheAsync(entry.FilePath);
    }
}
```

### 2. ユーザーインタフェース

ユーザーにキャッシュ削除オプションを提供：

```csharp
// UI: 設定画面
Button("Clear Scan Cache", onClick: async () =>
{
    await _metadataService.ClearAllScanCacheAsync();
    MessageBox.Show("Scan cache cleared.");
});
```

### 3. デバッグ・トラブルシューティング

スキャン結果が予期しない場合、キャッシュをクリアして再スキャン：

```csharp
await _metadataService.ClearScanCacheByRootAsync("D:\\Assets");
await _fileScannerService.ScanWithCacheAsync(new[] { "D:\\Assets" });
```

---

## 今後の拡張案

1. **キャッシュの永続化統計**: キャッシュヒット率・削減時間をログ記録
2. **増分スキャンのスケジューリング**: 定期的な増分スキャン（夜間など）
3. **キャッシュ有効期限の設定**: キャッシュレコードに TTL を付与
4. **分散スキャン**: 複数ディレクトリの並列スキャン時にキャッシュ競合を回避

