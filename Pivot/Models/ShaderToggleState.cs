namespace Pivot.Models
{
    /// <summary>
    /// Tracks individual shader toggle states for the 3D viewer.
    /// Material is always the base; other effects can be toggled on/off.
    /// </summary>
    public class ShaderToggleState
    {
        /// <summary>
        /// Whether to apply texture mapping on top of material
        /// </summary>
        public bool UseTexture { get; set; } = false;

        /// <summary>
        /// Whether to blend vertex colors with material
        /// </summary>
        public bool UseVertexColor { get; set; } = false;

        /// <summary>
        /// Whether to show UV checker pattern overlay
        /// </summary>
        public bool UseUVChecker { get; set; } = false;
    }
}
