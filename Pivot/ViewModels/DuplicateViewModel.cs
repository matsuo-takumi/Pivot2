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

// 画像表示用の軽量ViewModel
public class DuplicateAssetItem
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ThumbnailPath { get; set; } = string.Empty; // ここにサムネイルパスを入れる
}

public partial class DuplicateViewModel : ObservableObject
{
    private readonly IDbContextFactory<PivotDbContext> _dbFactory;

    [ObservableProperty]
    private ObservableCollection<DuplicateGroupViewModel> _duplicateGroups = new();

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusMessage = "Ready to scan";

    public DuplicateViewModel(IDbContextFactory<PivotDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsScanning) return;
        IsScanning = true;
        StatusMessage = "Scanning hashes...";
        DuplicateGroups.Clear();

        try
        {
            // DB操作は別スレッドで
            var groupsData = await Task.Run(async () =>
            {
                using var db = await _dbFactory.CreateDbContextAsync();
                
                // ハッシュが重複しているものを取得
                var duplicates = await db.Assets
                    .Where(a => a.PerceptualHash != null)
                    .GroupBy(a => a.PerceptualHash)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Select(a => new DuplicateAssetItem 
                    { 
                        Id = a.Id, 
                        FileName = a.FileName, 
                        FilePath = a.FilePath,
                        // 本来はThumbnailServiceを使うべきですが、簡易的にFilePathまたはキャッシュパスを指定
                        ThumbnailPath = a.FilePath 
                    }).ToList())
                    .ToListAsync();

                return duplicates;
            });

            // UIスレッドでViewModel構築
            foreach (var groupAssets in groupsData)
            {
                var groupVm = new DuplicateGroupViewModel(groupAssets);
                DuplicateGroups.Add(groupVm);
            }

            StatusMessage = $"Found {DuplicateGroups.Count} duplicate groups.";
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

public partial class DuplicateGroupViewModel : ObservableObject
{
    // XAMLが参照していた Assets プロパティ
    public ObservableCollection<DuplicateAssetItem> Assets { get; }

    // XAMLが参照していた SelectedToKeep プロパティ
    [ObservableProperty]
    private DuplicateAssetItem? _selectedToKeep;

    public DuplicateGroupViewModel(List<DuplicateAssetItem> assets)
    {
        Assets = new ObservableCollection<DuplicateAssetItem>(assets);
        // デフォルトで最初の一つを選択して残す候補にする
        SelectedToKeep = Assets.FirstOrDefault();
    }

    [RelayCommand]
    private async Task ResolveAsync()
    {
        if (SelectedToKeep == null) return;

        // ここに「選択されていないものを削除する」ロジックを実装
        // 今回はプレースホルダーのみ
        await Task.CompletedTask;
        
        // 処理完了後、親のリストから自分を消すなどの通知が必要
    }
}
