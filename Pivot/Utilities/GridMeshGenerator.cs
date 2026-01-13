using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pivot.Utilities
{
    /// <summary>
    /// Generates grid mesh data for floor display in 3D viewer.
    /// </summary>
    public static class GridMeshGenerator
    {
        /// <summary>
        /// Generate a flat grid mesh on the XZ plane (Y=0).
        /// Lines are created as thin quads for visibility.
        /// </summary>
        /// <param name="size">Total size of the grid (e.g., 10 = -5 to +5)</param>
        /// <param name="divisions">Number of divisions per axis</param>
        /// <param name="lineWidth">Width of grid lines</param>
        /// <returns>Vertex and index arrays for the grid mesh</returns>
        public static (Vertex[] Vertices, uint[] Indices) GenerateGrid(float size = 10f, int divisions = 10, float lineWidth = 0.02f)
        {
            var vertices = new List<Vertex>();
            var indices = new List<uint>();
            
            float halfSize = size / 2f;
            float step = size / divisions;
            
            // Grid color - very subtle, modern aesthetic (low opacity for thin appearance)
            var gridColor = new Vector4(0.25f, 0.25f, 0.28f, 0.6f);
            
            uint vertexIndex = 0;
            
            // Generate lines parallel to X axis
            for (int i = 0; i <= divisions; i++)
            {
                float z = -halfSize + i * step;
                float halfWidth = lineWidth / 2f;
                
                // Create a thin quad for each line
                vertices.Add(new Vertex { Position = new Vector3(-halfSize, 0, z - halfWidth), Normal = Vector3.UnitY, TexCoord = Vector2.Zero, Color = gridColor });
                vertices.Add(new Vertex { Position = new Vector3(halfSize, 0, z - halfWidth), Normal = Vector3.UnitY, TexCoord = Vector2.UnitX, Color = gridColor });
                vertices.Add(new Vertex { Position = new Vector3(halfSize, 0, z + halfWidth), Normal = Vector3.UnitY, TexCoord = Vector2.One, Color = gridColor });
                vertices.Add(new Vertex { Position = new Vector3(-halfSize, 0, z + halfWidth), Normal = Vector3.UnitY, TexCoord = Vector2.UnitY, Color = gridColor });
                
                // Two triangles per quad
                indices.Add(vertexIndex);
                indices.Add(vertexIndex + 1);
                indices.Add(vertexIndex + 2);
                indices.Add(vertexIndex);
                indices.Add(vertexIndex + 2);
                indices.Add(vertexIndex + 3);
                
                vertexIndex += 4;
            }
            
            // Generate lines parallel to Z axis
            for (int i = 0; i <= divisions; i++)
            {
                float x = -halfSize + i * step;
                float halfWidth = lineWidth / 2f;
                
                vertices.Add(new Vertex { Position = new Vector3(x - halfWidth, 0, -halfSize), Normal = Vector3.UnitY, TexCoord = Vector2.Zero, Color = gridColor });
                vertices.Add(new Vertex { Position = new Vector3(x + halfWidth, 0, -halfSize), Normal = Vector3.UnitY, TexCoord = Vector2.UnitX, Color = gridColor });
                vertices.Add(new Vertex { Position = new Vector3(x + halfWidth, 0, halfSize), Normal = Vector3.UnitY, TexCoord = Vector2.One, Color = gridColor });
                vertices.Add(new Vertex { Position = new Vector3(x - halfWidth, 0, halfSize), Normal = Vector3.UnitY, TexCoord = Vector2.UnitY, Color = gridColor });
                
                indices.Add(vertexIndex);
                indices.Add(vertexIndex + 1);
                indices.Add(vertexIndex + 2);
                indices.Add(vertexIndex);
                indices.Add(vertexIndex + 2);
                indices.Add(vertexIndex + 3);
                
                vertexIndex += 4;
            }
            
            return (vertices.ToArray(), indices.ToArray());
        }
    }
}
