using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pivot.Engine.Models;
using Pivot.Engine.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Pivot.ViewModels
{
    /// <summary>
    /// Partial class for ImageViewModel - Duplicate Detection Feature
    /// </summary>
    public partial class ImageViewModel
    {
        // ▼▼▼ Duplicate Feature Properties ▼▼▼
        
        [ObservableProperty]
        private bool _isDuplicateMode;

        [ObservableProperty]
        private ObservableCollection<DuplicateGroupViewModel> _duplicateGroups = new();

        [ObservableProperty]
        private bool _isScanningDuplicates;

        [ObservableProperty]
        private string _duplicateStatusMessage = "Ready";

        // ▼▼▼ Duplicate Feature Commands ▼▼▼

        [RelayCommand]
        private async Task EnterDuplicateModeAsync()
        {
            IsDuplicateMode = true;
            await ScanDuplicatesAsync();
        }

        [RelayCommand]
        private void ExitDuplicateMode()
        {
            IsDuplicateMode = false;
            DuplicateGroups.Clear();
            DuplicateStatusMessage = "Ready";
        }

        // ▼▼▼ Internal Logic ▼▼▼

        private async Task ScanDuplicatesAsync()
        {
            if (_analysisService == null)
            {
                DuplicateStatusMessage = "Error: Analysis service not available.";
                return;
            }

            if (IsScanningDuplicates) return;

            IsScanningDuplicates = true;
            DuplicateStatusMessage = "Scanning for duplicates...";
            DuplicateGroups.Clear();

            try
            {
                // Call Engine service (threshold=0 for exact match)
                var groups = await _analysisService.FindDuplicatesAsync(threshold: 0);

                if (groups.Count == 0)
                {
                    DuplicateStatusMessage = "No duplicates found.";
                }
                else
                {
                    // Convert to ViewModels
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        foreach (var group in groups)
                        {
                            DuplicateGroups.Add(new DuplicateGroupViewModel(group));
                        }
                        DuplicateStatusMessage = $"Found {groups.Count} duplicate groups.";
                    });
                }
            }
            catch (Exception ex)
            {
                DuplicateStatusMessage = $"Error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[ImageViewModel] Duplicate scan error: {ex}");
            }
            finally
            {
                IsScanningDuplicates = false;
            }
        }
    }
}
