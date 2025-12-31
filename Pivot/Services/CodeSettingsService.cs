using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Pivot.Services
{
    /// <summary>
    /// コード関連設定の管理サービス。
    /// エクスポート設定、最後に選択したスニペットなどを管理。
    /// </summary>
    public class CodeSettingsService
    {
        private readonly ILogger<CodeSettingsService> _logger;
        private readonly ISettingsStore _settingsStore;

        // In-memory cache
        private string _exportOutputDirectory = string.Empty;
        private string _codeExportFormat = "txt";
        private Guid? _lastSelectedSnippetId = null;

        public CodeSettingsService(
            ILogger<CodeSettingsService> logger,
            ISettingsStore settingsStore)
        {
            _logger = logger;
            _settingsStore = settingsStore;
        }

        public async Task LoadAsync()
        {
            _logger.LogInformation("CodeSettingsService: Loading...");
            try
            {
                // Export output directory
                var exportDir = await _settingsStore.GetAsync("Code.ExportOutputDirectory");
                if (!string.IsNullOrEmpty(exportDir))
                    _exportOutputDirectory = exportDir;

                // Code export format
                var format = await _settingsStore.GetAsync("Code.ExportFormat");
                if (!string.IsNullOrEmpty(format))
                    _codeExportFormat = format;

                // Last selected snippet ID
                var snippetId = await _settingsStore.GetAsync("Code.LastSelectedSnippetId");
                if (Guid.TryParse(snippetId, out var id))
                    _lastSelectedSnippetId = id;

                _logger.LogInformation("CodeSettingsService: Loaded");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CodeSettingsService: Failed to load, using defaults.");
            }
        }

        // =============== Export Output Directory ===============

        public string GetExportOutputDirectory() => _exportOutputDirectory;

        public async Task SetExportOutputDirectoryAsync(string path)
        {
            _exportOutputDirectory = path ?? string.Empty;
            try
            {
                await _settingsStore.UpsertAsync("Code.ExportOutputDirectory", _exportOutputDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CodeSettingsService: Failed to persist ExportOutputDirectory.");
            }
        }

        // =============== Code Export Format ===============

        public string GetCodeExportFormat() => _codeExportFormat;

        public async Task SetCodeExportFormatAsync(string format)
        {
            _codeExportFormat = format ?? "txt";
            try
            {
                await _settingsStore.UpsertAsync("Code.ExportFormat", _codeExportFormat);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CodeSettingsService: Failed to persist CodeExportFormat.");
            }
        }

        // =============== Last Selected Snippet ID ===============

        public Guid? GetLastSelectedSnippetId() => _lastSelectedSnippetId;

        public async Task SetLastSelectedSnippetIdAsync(Guid? id)
        {
            _lastSelectedSnippetId = id;
            try
            {
                var value = id.HasValue ? id.Value.ToString() : string.Empty;
                await _settingsStore.UpsertAsync("Code.LastSelectedSnippetId", value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CodeSettingsService: Failed to persist LastSelectedSnippetId.");
            }
        }
    }
}
