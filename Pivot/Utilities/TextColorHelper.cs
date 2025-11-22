using Microsoft.UI;
using System;
using Windows.UI;

namespace Pivot.Utilities
{
    internal static class TextColorHelper
    {
        public static string FormatHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        public static Color ParseHexOrDefault(string? hex, Color fallback)
        {
            if (TryParseHex(hex, out var parsed))
            {
                return parsed;
            }
            return fallback;
        }

        public static bool TryParseHex(string? value, out Color result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(value)) return false;
            try
            {
                var trimmed = value.StartsWith("#") ? value : "#" + value;
                if (trimmed.Length != 7) return false;
                result = ColorHelper.FromArgb(255,
                    Convert.ToByte(trimmed.Substring(1, 2), 16),
                    Convert.ToByte(trimmed.Substring(3, 2), 16),
                    Convert.ToByte(trimmed.Substring(5, 2), 16));
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

