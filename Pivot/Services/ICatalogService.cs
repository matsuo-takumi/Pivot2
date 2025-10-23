using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pivot.Models;

namespace Pivot.Services
{
	public interface ICatalogService
	{
		Task InitializeAsync(IEnumerable<string> roots, CancellationToken ct = default);
		IAsyncEnumerable<AssetInfo> StreamAssets(string? category = null, int batch = 500, CancellationToken ct = default);
		Task<IReadOnlyList<AssetInfo>> QueryByTagAsync(string tag, int skip, int take, CancellationToken ct = default);
		Task<AssetInfo?> GetByPathAsync(string path, CancellationToken ct = default);
		Task<AssetMeta?> GetMetaAsync(string path, CancellationToken ct = default);
		Task UpsertMetaAsync(string path, AssetMeta meta, CancellationToken ct = default);
		Task AddTagAsync(string path, string tag, CancellationToken ct = default);
		Task RemoveTagAsync(string path, string tag, CancellationToken ct = default);
	}
}


