using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Pivot.Services
{
    /// <summary>
    /// ソートフィールド
    /// </summary>
    public enum SortField
    {
        Name,
        Date,
        Size,
        Type,
        Rating,
        AspectRatio
    }

    /// <summary>
    /// ソート方向
    /// </summary>
    public enum SortDirection
    {
        Ascending,
        Descending
    }

    /// <summary>
    /// ソート処理を担当するサービス
    /// </summary>
    public class SortService
    {
        /// <summary>
        /// TemplateItem のコレクションをソート
        /// </summary>
        public IEnumerable<Pivot.Models.TemplateItem> Sort(
            IEnumerable<Pivot.Models.TemplateItem> items, 
            SortField field, 
            SortDirection direction)
        {
            if (items == null) return Enumerable.Empty<Pivot.Models.TemplateItem>();

            IOrderedEnumerable<Pivot.Models.TemplateItem> ordered = field switch
            {
                SortField.Name => direction == SortDirection.Ascending
                    ? items.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    : items.OrderByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase),
                    
                SortField.Date => direction == SortDirection.Ascending
                    ? items.OrderBy(x => GetFileDate(x.Path))
                    : items.OrderByDescending(x => GetFileDate(x.Path)),
                    
                SortField.Size => direction == SortDirection.Ascending
                    ? items.OrderBy(x => GetFileSize(x.Path))
                    : items.OrderByDescending(x => GetFileSize(x.Path)),
                    
                SortField.Type => direction == SortDirection.Ascending
                    ? items.OrderBy(x => Path.GetExtension(x.Path ?? ""), StringComparer.OrdinalIgnoreCase)
                    : items.OrderByDescending(x => Path.GetExtension(x.Path ?? ""), StringComparer.OrdinalIgnoreCase),
                    
                _ => items.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            };

            return ordered;
        }

        /// <summary>
        /// ソートフィールドの表示名を取得
        /// </summary>
        public string GetSortFieldName(SortField field)
        {
            return field switch
            {
                SortField.Name => "名前",
                SortField.Date => "日付",
                SortField.Size => "サイズ",
                SortField.Type => "種類",
                _ => "名前"
            };
        }

        /// <summary>
        /// ソートフィールドに対応するアイコンを取得
        /// </summary>
        public string GetSortIcon(SortField field)
        {
            return field switch
            {
                SortField.Name => "\uE8AC",     // Font
                SortField.Date => "\uE787",     // Calendar
                SortField.Size => "\uE7C1",     // Size
                SortField.Type => "\uE8A5",     // Document
                _ => "\uE8AC"
            };
        }

        /// <summary>
        /// ソート方向に対応するアイコンを取得
        /// </summary>
        public string GetDirectionIcon(SortDirection direction)
        {
            return direction switch
            {
                SortDirection.Ascending => "\uE74A",  // Up
                SortDirection.Descending => "\uE74B", // Down
                _ => "\uE74A"
            };
        }

        private static DateTime GetFileDate(string? path)
        {
            if (string.IsNullOrEmpty(path)) return DateTime.MinValue;
            try
            {
                return File.Exists(path) ? File.GetLastWriteTime(path) : DateTime.MinValue;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private static long GetFileSize(string? path)
        {
            if (string.IsNullOrEmpty(path)) return 0;
            try
            {
                return File.Exists(path) ? new FileInfo(path).Length : 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
