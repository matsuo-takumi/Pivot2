using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pivot.Services
{
    /// <summary>
    /// UI依存のダイアログ処理を抽象化するインターフェース。
    /// ViewModelから直接FolderPicker等を呼び出さず、このサービスを経由する。
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// フォルダ選択ダイアログを表示し、選択されたフォルダのパスを返す。
        /// キャンセルの場合はnullを返す。
        /// </summary>
        Task<string?> PickFolderAsync();

        /// <summary>
        /// ファイル選択ダイアログを表示し、選択されたファイルのパスを返す。
        /// キャンセルの場合はnullを返す。
        /// </summary>
        /// <param name="extensions">許可するファイル拡張子（例: ".txt", ".json"）</param>
        Task<string?> PickFileAsync(IEnumerable<string>? extensions = null);

        /// <summary>
        /// 複数ファイル選択ダイアログを表示し、選択されたファイルのパスリストを返す。
        /// キャンセルの場合は空のリストを返す。
        /// </summary>
        /// <param name="extensions">許可するファイル拡張子</param>
        Task<IReadOnlyList<string>> PickFilesAsync(IEnumerable<string>? extensions = null);
    }
}
