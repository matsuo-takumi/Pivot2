namespace Pivot.Models
{
    /// <summary>
    /// Shading modes available in the 3D viewer
    /// </summary>
    public enum ShadingMode
    {
        /// <summary>
        /// RGB representation of world-space surface normals
        /// </summary>
        WorldNormal = 0,

        /// <summary>
        /// Grayscale based on distance from camera
        /// </summary>
        Depth = 1,

        /// <summary>
        /// Simple directional lighting (grayscale)
        /// </summary>
        Lit = 2,

        /// <summary>
        /// Checkerboard pattern based on UV coordinates
        /// </summary>
        UV = 3
    }
}
