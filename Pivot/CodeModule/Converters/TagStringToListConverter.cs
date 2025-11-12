using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Data;
using Pivot.CodeModule.Models;

namespace Pivot.CodeModule.Converters
{
    public class TagStringToListConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            try
            {
                var s = value as string ?? string.Empty;
                var list = s.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select((n, idx) => new TagItem { Id = idx, Name = n, IsSelected = true })
                    .ToList();
                return list;
            }
            catch
            {
                return new List<TagItem>();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            try
            {
                if (value is IEnumerable<TagItem> items)
                {
                    return string.Join(",", items.Select(i => i.Name).Where(n => !string.IsNullOrWhiteSpace(n)));
                }
            }
            catch { }
            return string.Empty;
        }
    }
}


