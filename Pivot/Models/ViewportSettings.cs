using System.Numerics;
using Windows.UI;

namespace Pivot.Models
{
    /// <summary>
    /// Viewport display settings for 3D viewer
    /// </summary>
    public class ViewportSettings
    {
        public Color BackgroundColor { get; set; } = Color.FromArgb(255, 51, 153, 204);
        public Vector3 LightDirection { get; set; } = Vector3.Normalize(new Vector3(0.5f, 1.0f, 0.3f));
        public float LightIntensity { get; set; } = 1.0f;
        public Color LightColor { get; set; } = Color.FromArgb(255, 255, 255, 255); // White
    }
}
