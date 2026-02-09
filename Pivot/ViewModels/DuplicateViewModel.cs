using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Pivot.Engine.Data;
using Pivot.Engine.Models;

namespace Pivot.ViewModels;

public partial class DuplicateViewModel : ObservableObject
{
    private readonly IDbContextFactory<PivotDbContext> _dbFactory;

    // 重複グループのリスト
    [ObservableProperty]
    private ObservableCollection<DuplicateGroupViewModel> _duplicateGroups = new();

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusMessage = "Ready to scan";

    // コンストラクタで DB Factory を受け取る (DI)
    public DuplicateViewModel(IDbContextFactory<PivotDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        IsScanning = true;
        StatusMessage = "Scanning for duplicates...";
        DuplicateGroups.Clear();

        try
        {
            await Task.Run(async () =>
            {
                using var db = await _dbFactory.CreateDbContextAsync();

                // ハッシュがある画像のみ取得
                var assets = await db.Assets
                    .Where(a => a.PerceptualHash != null)
                    .Select(a => new { a.Id, a.PerceptualHash })
                    .ToListAsync();

                // ここでハッシュ比較ロジック (簡易実装: 完全一致)
                // ※ 本来はハミング距離計算が必要ですが、まずは完全一致で動かします
                var groups = assets
                    .GroupBy(a => a.PerceptualHash)
                    .Where(g => g.Count() > 1)
                    .ToList();

                foreach (var group in groups)
                {
                    // UIスレッドでコレクションに追加
                    // (実際のアプリではここでもう少し詳細なデータをロードします)
                    var groupVm = new DuplicateGroupViewModel(group.Select(x => x.Id).ToList());
                    
                    // UI更新のためDispatcherが必要ですが、MVVM ToolkitのMessenger等を使うか
                    // 簡易的にUIスレッドへ戻す処理が必要です。
                    // ここでは簡略化のため、ViewModel側でコレクション操作だけに留めます。
                    // 注意: Task.Run内から直接ObservableCollectionを操作すると例外が発生する可能性があります。
                    // 実際のアプリでは DispatcherQueue を使うべきですが、
                    // ここではユーザー提示コードに従いつつ、BindingOperations.EnableCollectionSynchronization等の対策が前提、
                    // またはUIスレッドに戻して追加する必要があります。
                    // 今回は簡易的に、ここでリストを作って最後に一括更新するか、
                    // RelayCommandの非同期フロー(UIスレッドに戻ってくる)を利用して、
                    // 結果を返してから追加するのがベターですが、
                    // ユーザーコードを尊重しつつ、ObservableCollection操作はUIスレッドで行うように修正します。
                }
                
                return groups; // グループ情報を返す
            }).ContinueWith(t => 
            {
                if (t.IsFaulted) throw t.Exception;
                
                var groups = t.Result;
                foreach(var g in groups)
                {
                     DuplicateGroups.Add(new DuplicateGroupViewModel(g.Select(x => x.Id).ToList()));
                }
            }, TaskScheduler.FromCurrentSynchronizationContext()); // UIスレッドで実行
            
            StatusMessage = "Scan complete.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }
}

// グループ用の子ViewModel
public partial class DuplicateGroupViewModel : ObservableObject
{
    public List<int> AssetIds { get; }

    public DuplicateGroupViewModel(List<int> assetIds)
    {
        AssetIds = assetIds;
    }

    [RelayCommand]
    private async Task ResolveAsync()
    {
        // 解決（削除など）ロジック
        await Task.CompletedTask;
    }
}
