using System;
using Windows.UI;

namespace Pivot.Utilities
{
    /// <summary>
    /// Helper class for parsing color strings.
    /// </summary>
    public static class ColorHelper
    {
        public static Windows.UI.Color ParseColor(string? hexColor)
        {
            if (string.IsNullOrEmpty(hexColor) || !hexColor.StartsWith("#"))
            {
                return Windows.UI.Color.FromArgb(255, 64, 64, 64); // Default dark gray
            }

            try
            {
                hexColor = hexColor.TrimStart('#');
                if (hexColor.Length == 6)
                {
                    byte r = Convert.ToByte(hexColor.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hexColor.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hexColor.Substring(4, 2), 16);
                    return Windows.UI.Color.FromArgb(255, r, g, b);
                }
            }
            catch { }

            return Windows.UI.Color.FromArgb(255, 64, 64, 64);
        }
    }
}
