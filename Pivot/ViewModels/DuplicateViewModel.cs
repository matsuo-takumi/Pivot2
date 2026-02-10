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
                    .AsNoTracking()
                    .Where(a => a.PerceptualHash != null)
                    .ToListAsync();

                // ここでハッシュ比較ロジック (簡易実装: 完全一致)
                // ※ 本来はハミング距離計算が必要ですが、まずは完全一致で動かします
                var groups = assets
                    .GroupBy(a => a.PerceptualHash)
                    .Where(g => g.Count() > 1)
                    .Select(g => new DuplicateGroupViewModel(g.ToList()))
                    .ToList();
                
                return groups; // グループ情報を返す
            }).ContinueWith(t => 
            {
                if (t.IsFaulted) throw t.Exception;
                
                var groups = t.Result;
                foreach(var g in groups)
                {
                     DuplicateGroups.Add(g);
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
    [ObservableProperty]
    private ObservableCollection<AssetEntity> _assets;

    [ObservableProperty]
    private AssetEntity? _selectedToKeep;

    public DuplicateGroupViewModel(List<AssetEntity> assets)
    {
        Assets = new ObservableCollection<AssetEntity>(assets);
        SelectedToKeep = assets.FirstOrDefault();
    }

    [RelayCommand]
    private async Task ResolveAsync()
    {
        // 解決（削除など）ロジック
        await Task.CompletedTask;
    }
}
