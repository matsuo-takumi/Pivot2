using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pivot.Models;

namespace Pivot.Services
{
    /// <summary>
    /// アセットファイルのデータアクセスインターフェース。
    /// SQLiteデータベースからのアセットメタデータの読み書きを提供します。
    /// </summary>
    public interface IAssetRepository
    {
        // =============== Read Operations ===============

        /// <summary>
        /// アセットを取得（ページング対応）
        /// </summary>
        /// <param name="directories">フィルタするディレクトリ（nullで全件）</param>
        /// <param name="kind">フィルタするアセット種別（nullで全種別）</param>
        /// <param name="skip">スキップする件数</param>
        /// <param name="take">取得する件数</param>
        /// <param name="orderBy">ソートカラム（FileName, LastModifiedTicks, FileSize）</param>
        /// <param name="descending">降順ソートするか</param>
        /// <param name="ct">キャンセルトークン</param>
        Task<List<AssetFile>> GetAssetsAsync(
            IEnumerable<string>? directories = null,
            AssetKind? kind = null,
            int skip = 0,
            int take = 50,
            string orderBy = "FileName",
            bool descending = false,
            CancellationToken ct = default);

        /// <summary>
        /// アセットの総件数を取得
        /// </summary>
        Task<int> GetCountAsync(
            IEnumerable<string>? directories = null,
            AssetKind? kind = null,
            CancellationToken ct = default);

        /// <summary>
        /// ファイルパスからアセットを取得
        /// </summary>
        Task<AssetFile?> GetByPathAsync(string filePath, CancellationToken ct = default);

        /// <summary>
        /// 同期用: 指定ディレクトリ内の全ファイルパスと更新日時のマップを取得
        /// </summary>
        Task<Dictionary<string, long>> GetFileMapAsync(
            IEnumerable<string> directories,
            CancellationToken ct = default);

        /// <summary>
        /// 未インデックスのアセットを取得（メタデータ抽出用）
        /// </summary>
        Task<List<AssetFile>> GetUnindexedAssetsAsync(int take = 100, CancellationToken ct = default);

        // =============== Write Operations ===============

        /// <summary>
        /// アセットを追加
        /// </summary>
        Task AddAsync(AssetFile asset, CancellationToken ct = default);

        /// <summary>
        /// アセットを一括追加
        /// </summary>
        Task AddRangeAsync(IEnumerable<AssetFile> assets, CancellationToken ct = default);

        /// <summary>
        /// アセットを更新
        /// </summary>
        Task UpdateAsync(AssetFile asset, CancellationToken ct = default);

        /// <summary>
        /// アセットを一括更新
        /// </summary>
        Task UpdateRangeAsync(IEnumerable<AssetFile> assets, CancellationToken ct = default);

        /// <summary>
        /// ファイルパスでアセットを削除（物理削除）
        /// </summary>
        Task DeleteAsync(string filePath, CancellationToken ct = default);

        /// <summary>
        /// 複数アセットを一括削除
        /// </summary>
        Task DeleteRangeAsync(IEnumerable<string> filePaths, CancellationToken ct = default);

        /// <summary>
        /// 指定ディレクトリ配下のアセットを論理削除
        /// </summary>
        Task MarkDeletedByDirectoryAsync(string directory, CancellationToken ct = default);

        /// <summary>
        /// 論理削除されたアセットを物理削除
        /// </summary>
        Task PurgeDeletedAsync(CancellationToken ct = default);
    }
}
