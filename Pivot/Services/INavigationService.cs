using System;
using Pivot.Engine.Models;
using Pivot.Models;

namespace Pivot.Services
{
    /// <summary>
    /// ナビゲーション抽象化インターフェース。
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// 指定された領域にナビゲートする。
        /// </summary>
        void NavigateTo(NavigationRegion region);

        /// <summary>
        /// ナビゲート処理を実行するデリゲートを設定する。
        /// MainWindowから呼び出され、実際のFrame.Navigate処理を登録する。
        /// </summary>
        void RegisterNavigationHandler(Action<NavigationRegion> handler);
    }
}
