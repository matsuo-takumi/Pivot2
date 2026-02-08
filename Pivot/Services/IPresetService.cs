using Pivot.Engine.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pivot.Services
{
    /// <summary>
    /// 汎用的なプリセットサービスのインターフェース
    /// </summary>
    public interface IPresetService<T>
    {
        /// <summary>
        /// すべてのプリセットを取得
        /// </summary>
        Task<IReadOnlyList<Preset<T>>> GetAllPresetsAsync();

        /// <summary>
        /// プリセットを保存
        /// </summary>
        Task<Preset<T>> SavePresetAsync(Preset<T> preset);

        /// <summary>
        /// プリセットを削除
        /// </summary>
        Task<bool> DeletePresetAsync(string presetId);

        /// <summary>
        /// プリセットを取得
        /// </summary>
        Task<Preset<T>?> GetPresetAsync(string presetId);

        /// <summary>
        /// プリセット名の重複チェック
        /// </summary>
        Task<bool> IsNameExistsAsync(string name, string? excludeId = null);
    }
}





