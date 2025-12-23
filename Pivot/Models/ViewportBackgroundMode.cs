namespace Pivot.Models
{
    /// <summary>
    /// Viewport background color mode
    /// </summary>
    public enum ViewportBackgroundMode
    {
        /// <summary>
        /// User-specified custom color
        /// </summary>
        Custom,
        
        /// <summary>
        /// Match window theme (Mica) - dark theme = dark bg, light theme = light bg
        /// </summary>
        MatchTheme
    }
}
