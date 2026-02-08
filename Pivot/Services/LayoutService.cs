using Pivot.Engine.Models;

namespace Pivot.Services
{
    /// <summary>
    /// レイアウトモードの管理を担当するサービス
    /// </summary>
    public class LayoutService
    {
        // Grid, List, Masonry の3種類
        private static readonly LayoutType[] _layoutCycle = { LayoutType.Grid, LayoutType.List, LayoutType.Masonry };

        /// <summary>
        /// 次のレイアウトモードを取得
        /// </summary>
        public LayoutType GetNextLayout(LayoutType current)
        {
            var currentIndex = System.Array.IndexOf(_layoutCycle, current);
            if (currentIndex < 0) currentIndex = 0;
            var nextIndex = (currentIndex + 1) % _layoutCycle.Length;
            return _layoutCycle[nextIndex];
        }

        /// <summary>
        /// レイアウトモードに対応するアイコン（Segoe MDL2 Assets）を取得
        /// </summary>
        public string GetLayoutIcon(LayoutType layout)
        {
            return layout switch
            {
                LayoutType.Grid => "\uF0E2",    // GridView
                LayoutType.List => "\uE8FD",    // List
                LayoutType.Masonry => "\uE80A", // Photo Gallery
                _ => "\uF0E2"
            };
        }

        /// <summary>
        /// レイアウトモードの表示名を取得
        /// </summary>
        public string GetLayoutName(LayoutType layout)
        {
            return layout switch
            {
                LayoutType.Grid => "Grid",
                LayoutType.List => "List",
                LayoutType.Masonry => "Masonry",
                _ => "Grid"
            };
        }
    }
}
