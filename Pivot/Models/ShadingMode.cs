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
        UV = 3,

        /// <summary>
        /// Physically-based rendering with material properties
        /// </summary>
        Material = 4,

        /// <summary>
        /// Display vertex colors from the model
        /// </summary>
        VertexColor = 5,

        /// <summary>
        /// UV-mapped texture display
        /// </summary>
        Texture = 6,

        /// <summary>
        /// Blend mode: Blends enabled toggles (Texture, Material, Vertex Color, UV)
        /// </summary>
        Combined = 7
    }
}
