using System.Threading;
using System.Threading.Tasks;
using Pivot.Models;

namespace Pivot.Services.Engines
{
    /// <summary>
    /// Interface for asset processing engines.
    /// Engines are responsible for handling specific file types, extracting metadata,
    /// generating thumbnails, and performing other type-specific operations.
    /// </summary>
    public interface IAssetEngine
    {
        /// <summary>
        /// List of file extensions supported by this engine (e.g., ".jpg", ".png").
        /// Extensions should include the leading dot and be lowercased.
        /// </summary>
        string[] SupportedExtensions { get; }

        /// <summary>
        /// Processing priority of this engine. Higher values are processed first.
        /// Useful when multiple engines support the same extension (e.g., specific vs general).
        /// Default is 0.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Process a file to extract metadata and generate derivatives (thumbnails, etc.).
        /// This method is called during file scanning or when a file is modified.
        /// </summary>
        /// <param name="filePath">Full path to the file.</param>
        /// <param name="asset">The asset entity being updated. The engine should modify this object.</param>
        /// <param name="ct">Cancellation token.</param>
        Task ProcessFileAsync(string filePath, AssetEntity asset, CancellationToken ct = default);

        /// <summary>
        /// Checks if the existing asset is up to date (e.g., thumbnail exists).
        /// </summary>
        /// <param name="asset">The existing asset entity.</param>
        /// <returns>True if up to date, false if reprocessing is needed.</returns>
        bool IsUpToDate(AssetEntity asset);
    }
}
