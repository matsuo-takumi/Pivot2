using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pivot.Models;

namespace Pivot.Repositories
{
    public interface IAssetRepository
    {
        Task<List<AssetEntity>> GetPagedAsync(
            AssetKind? kind = null,
            string? directory = null,
            string? sortField = "LastModifiedUtc",
            bool ascending = false,
            int skip = 0,
            int take = 100,
            CancellationToken ct = default);

        Task<int> GetCountAsync(
            AssetKind? kind = null,
            string? directory = null,
            CancellationToken ct = default);

        Task<AssetEntity?> GetByPathAsync(string filePath, CancellationToken ct = default);

        Task UpsertAsync(AssetEntity asset, CancellationToken ct = default);

        Task MarkDeletedAsync(string filePath, CancellationToken ct = default);

        Task<List<string>> GetAllDirectoriesAsync(
            AssetKind? kind = null,
            CancellationToken ct = default);

        /// <summary>
        /// Delete all assets in the specified directory and its subdirectories.
        /// </summary>
        Task<int> DeleteByDirectoryAsync(string directoryPath, CancellationToken ct = default);

        /// <summary>
        /// Get all existing assets under a directory for batch lookup (initial scan optimization).
        /// Returns dictionary keyed by FilePath for O(1) lookup.
        /// </summary>
        Task<Dictionary<string, AssetEntity>> GetExistingAssetsInDirectoryAsync(
            string directoryPath, 
            CancellationToken ct = default);

        /// <summary>
        /// Permanently delete an asset by file path (hard delete).
        /// </summary>
        Task HardDeleteByPathAsync(string filePath, CancellationToken ct = default);
    }
}
