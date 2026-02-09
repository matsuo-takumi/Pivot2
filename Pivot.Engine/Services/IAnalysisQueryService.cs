using Pivot.Engine.Models;

namespace Pivot.Engine.Services
{
    public interface IAnalysisQueryService
    {
        Task<List<AssetEntity>> SearchByColorAsync(byte r, byte g, byte b, int tolerance = 30, int maxResults = 100);
        
        /// <summary>
        /// Finds groups of duplicated assets.
        /// </summary>
        /// <param name="threshold">Hamming distance threshold (0 = exact, 1-3 = similar)</param>
        Task<List<List<AssetEntity>>> FindDuplicatesAsync(int threshold = 0);
    }
}
