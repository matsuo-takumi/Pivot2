using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pivot.Models;

namespace Pivot.Services
{
    /// <summary>
    /// Unified browser settings service.
    /// Consolidates ImageDisplaySettingsService, CodeSettingsService, and AssetDisplaySettingsService.
    /// </summary>
    public class BrowserSettingsService(
        ILogger<BrowserSettingsService> logger,
        ISettingsStore settingsStore)
    {
        private readonly ILogger<BrowserSettingsService> _logger = logger;
        private readonly ISettingsStore _settingsStore = settingsStore;

        // Layout settings
        private LayoutType _defaultLayoutMode = LayoutType.Grid;
        private double _gridItemWidth = 200;
        
        // Sort settings
        private SortField _defaultSortField = SortField.Date;
        private SortDirection _sortDirection = SortDirection.Descending;
        
        // Display settings
        private bool _showExtensions = false;
        private bool _showHiddenFiles = false;
        private bool _showMetadata = true;
        
        // Performance settings
        private int _loadLimit = 50;
        private bool _useHighQualityThumbnails = false;
        
        // Code specific (migrated from CodeSettingsService)
        private string _exportOutputDirectory = string.Empty;
        private Guid? _lastSelectedSnippetId = null;
        private string _codeExportFormat = "txt";

        public async Task LoadAsync()
        {
            _logger.LogInformation("BrowserSettingsService: Loading...");
            try
            {
                // Layout
                var layoutStr = await _settingsStore.GetAsync("Browser.LayoutMode");
                if (Enum.TryParse<LayoutType>(layoutStr, out var layout))
                    _defaultLayoutMode = layout;

                var itemWidth = await _settingsStore.GetAsync("Browser.GridItemWidth");
                if (double.TryParse(itemWidth, out var width))
                    _gridItemWidth = width;

                // Sort
                var sortFieldStr = await _settingsStore.GetAsync("Browser.SortField");
                if (Enum.TryParse<SortField>(sortFieldStr, out var field))
                    _defaultSortField = field;

                var sortDirStr = await _settingsStore.GetAsync("Browser.SortDirection");
                if (Enum.TryParse<SortDirection>(sortDirStr, out var dir))
                    _sortDirection = dir;

                // Display
                var showExt = await _settingsStore.GetAsync("Browser.ShowExtensions");
                if (bool.TryParse(showExt, out var ext)) _showExtensions = ext;

                var showHidden = await _settingsStore.GetAsync("Browser.ShowHiddenFiles");
                if (bool.TryParse(showHidden, out var hidden)) _showHiddenFiles = hidden;

                var showMeta = await _settingsStore.GetAsync("Browser.ShowMetadata");
                if (bool.TryParse(showMeta, out var meta)) _showMetadata = meta;

                // Performance
                var limit = await _settingsStore.GetAsync("Browser.LoadLimit");
                if (int.TryParse(limit, out var lim)) _loadLimit = lim;

                var hqThumbs = await _settingsStore.GetAsync("Browser.UseHighQualityThumbnails");
                if (bool.TryParse(hqThumbs, out var hq)) _useHighQualityThumbnails = hq;

                // Code specific
                var exportDir = await _settingsStore.GetAsync("Code.ExportOutputDirectory");
                if (!string.IsNullOrEmpty(exportDir)) _exportOutputDirectory = exportDir;

                var format = await _settingsStore.GetAsync("Code.ExportFormat");
                if (!string.IsNullOrEmpty(format)) _codeExportFormat = format;

                var snippetId = await _settingsStore.GetAsync("Code.LastSelectedSnippetId");
                if (Guid.TryParse(snippetId, out var id)) _lastSelectedSnippetId = id;

                _logger.LogInformation("BrowserSettingsService: Loaded");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "BrowserSettingsService: Failed to load, using defaults.");
            }
        }

        // =============== Layout ===============
        public LayoutType DefaultLayoutMode => _defaultLayoutMode;
        public async Task SetDefaultLayoutModeAsync(LayoutType mode)
        {
            _defaultLayoutMode = mode;
            await _settingsStore.UpsertAsync("Browser.LayoutMode", mode.ToString());
        }

        public double GridItemWidth => _gridItemWidth;
        public async Task SetGridItemWidthAsync(double width)
        {
            _gridItemWidth = width;
            await _settingsStore.UpsertAsync("Browser.GridItemWidth", width.ToString());
        }

        // =============== Sort ===============
        public SortField DefaultSortField => _defaultSortField;
        public async Task SetDefaultSortFieldAsync(SortField field)
        {
            _defaultSortField = field;
            await _settingsStore.UpsertAsync("Browser.SortField", field.ToString());
        }

        public SortDirection SortDirection => _sortDirection;
        public async Task SetSortDirectionAsync(SortDirection direction)
        {
            _sortDirection = direction;
            await _settingsStore.UpsertAsync("Browser.SortDirection", direction.ToString());
        }

        // =============== Display ===============
        public bool ShowExtensions => _showExtensions;
        public async Task SetShowExtensionsAsync(bool show)
        {
            _showExtensions = show;
            await _settingsStore.UpsertAsync("Browser.ShowExtensions", show.ToString());
        }

        public bool ShowHiddenFiles => _showHiddenFiles;
        public async Task SetShowHiddenFilesAsync(bool show)
        {
            _showHiddenFiles = show;
            await _settingsStore.UpsertAsync("Browser.ShowHiddenFiles", show.ToString());
        }

        public bool ShowMetadata => _showMetadata;
        public async Task SetShowMetadataAsync(bool show)
        {
            _showMetadata = show;
            await _settingsStore.UpsertAsync("Browser.ShowMetadata", show.ToString());
        }

        // =============== Performance ===============
        public int LoadLimit => _loadLimit;
        public async Task SetLoadLimitAsync(int limit)
        {
            _loadLimit = limit;
            await _settingsStore.UpsertAsync("Browser.LoadLimit", limit.ToString());
        }

        public bool UseHighQualityThumbnails => _useHighQualityThumbnails;
        public async Task SetUseHighQualityThumbnailsAsync(bool useHq)
        {
            _useHighQualityThumbnails = useHq;
            await _settingsStore.UpsertAsync("Browser.UseHighQualityThumbnails", useHq.ToString());
        }

        // =============== Code Specific ===============
        public string ExportOutputDirectory => _exportOutputDirectory;
        public async Task SetExportOutputDirectoryAsync(string path)
        {
            _exportOutputDirectory = path ?? string.Empty;
            await _settingsStore.UpsertAsync("Code.ExportOutputDirectory", _exportOutputDirectory);
        }

        public string CodeExportFormat => _codeExportFormat;
        public async Task SetCodeExportFormatAsync(string format)
        {
            _codeExportFormat = format ?? "txt";
            await _settingsStore.UpsertAsync("Code.ExportFormat", _codeExportFormat);
        }

        public Guid? LastSelectedSnippetId => _lastSelectedSnippetId;
        public async Task SetLastSelectedSnippetIdAsync(Guid? id)
        {
            _lastSelectedSnippetId = id;
            var value = id.HasValue ? id.Value.ToString() : string.Empty;
            await _settingsStore.UpsertAsync("Code.LastSelectedSnippetId", value);
        }
    }
}
