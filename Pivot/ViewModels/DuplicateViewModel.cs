using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pivot.Engine.Models;
using Pivot.Engine.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Pivot.ViewModels
{
    public partial class DuplicateViewModel : ObservableObject
    {
        private readonly IAnalysisQueryService _analysisService;
        
        [ObservableProperty]
        private bool _isScanning;

        [ObservableProperty]
        private string _statusText = "Ready to scan.";

        public ObservableCollection<DuplicateGroupViewModel> Groups { get; } = new();

        public DuplicateViewModel(IAnalysisQueryService analysisService)
        {
            _analysisService = analysisService;
        }

        [RelayCommand]
        private async Task ScanAsync()
        {
            if (IsScanning) return;

            IsScanning = true;
            StatusText = "Scanning for duplicates... (this may take a while)";
            Groups.Clear();

            try
            {
                // Threshold 0 = Exact match. 
                // We could expose threshold UI later.
                var results = await _analysisService.FindDuplicatesAsync(0);
                
                foreach (var group in results)
                {
                    if (group.Count > 1)
                    {
                        Groups.Add(new DuplicateGroupViewModel(group));
                    }
                }

                StatusText = $"Found {Groups.Count} groups of duplicates.";
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }
    }

    public partial class DuplicateGroupViewModel : ObservableObject
    {
        public ObservableCollection<AssetEntity> Assets { get; } = new();
        
        [ObservableProperty]
        private AssetEntity? _selectedToKeep;

        public DuplicateGroupViewModel(List<AssetEntity> assets)
        {
            foreach (var a in assets) Assets.Add(a);
            
            // Default: Keep the one with the earliest creation date or high res?
            // Logic: Filesize, then Date.
            SelectedToKeep = assets.OrderByDescending(a => a.FileSize).ThenBy(a => a.LastModifiedUtc).FirstOrDefault();
        }

        [RelayCommand]
        private async Task ResolveAsync()
        {
            if (SelectedToKeep == null) return;
            
            // Logic to delete others would go here. 
            // Since we don't have a centralized "AssetService.Delete" exposed here yet, 
            // we might need to inject it or fire an event.
            // For now, let's just remove from the list to simulate.
            
            // TODO: Call FileSystem/DB delete service.
            
            // For prototype:
            Assets.Clear();
            Assets.Add(SelectedToKeep);
        }
    }
}
