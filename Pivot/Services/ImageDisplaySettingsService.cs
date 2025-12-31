using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pivot.Models;

namespace Pivot.Services
{
    /// <summary>
    /// 画像表示設定の管理サービス。
    /// レイアウト、ソート設定を管理。
    /// </summary>
    public class ImageDisplaySettingsService
    {
        private readonly ILogger<ImageDisplaySettingsService> _logger;
        private readonly ISettingsStore _settingsStore;

        // In-memory cache
        private LayoutType _layoutMode = LayoutType.Grid;
        private SortField _sortField = SortField.Name;
        private SortDirection _sortDirection = SortDirection.Ascending;

        public ImageDisplaySettingsService(
            ILogger<ImageDisplaySettingsService> logger,
            ISettingsStore settingsStore)
        {
            _logger = logger;
            _settingsStore = settingsStore;
        }

        public async Task LoadAsync()
        {
            _logger.LogInformation("ImageDisplaySettingsService: Loading...");
            try
            {
                // Layout mode
                var layoutStr = await _settingsStore.GetAsync("Image.LayoutMode");
                if (Enum.TryParse<LayoutType>(layoutStr, out var layout))
                    _layoutMode = layout;

                // Sort field
                var sortFieldStr = await _settingsStore.GetAsync("Image.SortField");
                if (Enum.TryParse<SortField>(sortFieldStr, out var field))
                    _sortField = field;

                // Sort direction
                var sortDirStr = await _settingsStore.GetAsync("Image.SortDirection");
                if (Enum.TryParse<SortDirection>(sortDirStr, out var dir))
                    _sortDirection = dir;

                _logger.LogInformation("ImageDisplaySettingsService: Loaded");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImageDisplaySettingsService: Failed to load, using defaults.");
            }
        }

        // =============== Layout Mode ===============

        public LayoutType GetLayoutMode() => _layoutMode;

        public async Task SetLayoutModeAsync(LayoutType layout)
        {
            _layoutMode = layout;
            try
            {
                await _settingsStore.UpsertAsync("Image.LayoutMode", layout.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImageDisplaySettingsService: Failed to persist LayoutMode.");
            }
        }

        // =============== Sort Field ===============

        public SortField GetSortField() => _sortField;

        public async Task SetSortFieldAsync(SortField field)
        {
            _sortField = field;
            try
            {
                await _settingsStore.UpsertAsync("Image.SortField", field.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImageDisplaySettingsService: Failed to persist SortField.");
            }
        }

        // =============== Sort Direction ===============

        public SortDirection GetSortDirection() => _sortDirection;

        public async Task SetSortDirectionAsync(SortDirection direction)
        {
            _sortDirection = direction;
            try
            {
                await _settingsStore.UpsertAsync("Image.SortDirection", direction.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImageDisplaySettingsService: Failed to persist SortDirection.");
            }
        }
    }

    // Note: SortField and SortDirection are defined in SortService.cs
}
