using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Pivot.Services
{
	public interface ISettingsStore
	{
		Task InitializeAsync(CancellationToken ct = default);
		Task<string?> GetAsync(string key, CancellationToken ct = default);
		Task UpsertAsync(string key, string value, CancellationToken ct = default);
		Task DeleteAsync(string key, CancellationToken ct = default);
		Task<IDictionary<string, string>> GetAllAsync(CancellationToken ct = default);
	}
}


