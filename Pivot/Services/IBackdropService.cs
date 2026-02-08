using System;
using Microsoft.UI.Xaml;
using Pivot.Engine.Models;
using Pivot.Models;

namespace Pivot.Services
{
    /// <summary>
    /// バックドロップ（Mica/Acrylic/Overlay/None）管理のインターフェース。
    /// </summary>
    public interface IBackdropService
    {
        /// <summary>
        /// 現在のバックドロップタイプを取得する。
        /// </summary>
        BackdropType CurrentBackdropType { get; }

        /// <summary>
        /// バックドロップを設定する。
        /// </summary>
        /// <param name="window">対象ウィンドウ</param>
        /// <param name="type">バックドロップタイプ</param>
        /// <param name="rootElement">ルートUI要素（背景設定用）</param>
        /// <param name="titleBar">タイトルバー要素</param>
        void SetBackdrop(Window window, BackdropType type, FrameworkElement? rootElement, FrameworkElement? titleBar);

        /// <summary>
        /// リソースを解放する。
        /// </summary>
        void Dispose();

        /// <summary>
        /// 設定ソースを更新する（テーマ変更時）。
        /// </summary>
        void UpdateTheme(ElementTheme theme);

        /// <summary>
        /// ウィンドウのアクティブ状態を設定する。
        /// </summary>
        void SetInputActive(bool isActive);
    }
}
