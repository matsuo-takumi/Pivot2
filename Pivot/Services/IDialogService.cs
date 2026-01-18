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

        /// <summary>
        /// 複数フォルダ選択を可能にするダイアログを表示。
        /// ユーザーがキャンセルするまで連続して選択可能。
        /// </summary>
        Task<IReadOnlyList<string>> PickMultipleFoldersAsync();

        /// <summary>
        /// 確認ダイアログを表示し、ユーザーの選択（Yes/No）を返す。
        /// </summary>
        /// <param name="title">ダイアログのタイトル</param>
        /// <param name="message">ダイアログのメッセージ</param>
        /// <returns>Yesが選択された場合はtrue</returns>
        Task<bool> ShowConfirmationAsync(string title, string message);
    }
}
