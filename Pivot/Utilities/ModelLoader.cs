using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Silk.NET.Assimp;

namespace Pivot.Utilities
{
    /// <summary>
    /// Up-axis orientation
    /// </summary>
    public enum UpAxis
    {
        YUp,
        ZUp,
        Unknown
    }

    /// <summary>
    /// Axis-aligned bounding box
    /// </summary>
    public struct BoundingBox
    {
        public Vector3 Min;
        public Vector3 Max;
        
        public Vector3 Center => (Min + Max) * 0.5f;
        public Vector3 Size => Max - Min;
        public float Radius => Size.Length() * 0.5f;
        
        public static BoundingBox CreateFromPoints(IEnumerable<Vector3> points)
        {
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            
            foreach (var p in points)
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            
            return new BoundingBox { Min = min, Max = max };
        }
    }

    /// <summary>
    /// Loads 3D models using Assimp
    /// </summary>
    public unsafe class ModelLoader : IDisposable
    {
        private Assimp? _assimp;
        
        /// <summary>
        /// Target up-axis for normalization (default: Y-up to match camera)
        /// </summary>
        public UpAxis TargetUpAxis { get; set; } = UpAxis.YUp;
        
        public ModelLoader()
        {
            _assimp = Assimp.GetApi();
        }
        
        /// <summary>
        /// Load a 3D model from file
        /// </summary>
        public MeshData? LoadModel(string filePath)
        {
            if (_assimp == null)
                throw new ObjectDisposedException(nameof(ModelLoader));
            
            var scene = _assimp.ImportFile(filePath, 
                (uint)(PostProcessSteps.Triangulate | 
                       PostProcessSteps.GenerateNormals |
                       PostProcessSteps.FlipUVs |
                       PostProcessSteps.JoinIdenticalVertices));
            
            if (scene == null || scene->MFlags == (uint)SceneFlags.Incomplete || scene->MRootNode == null)
            {
                var error = _assimp.GetErrorStringS();
                throw new Exception($"Assimp error: {error}");
            }
            
            // Detect up-axis from file type
            var detectedUpAxis = DetectUpAxis(filePath, scene);
            
            var vertices = new List<Vertex>();
            var indices = new List<uint>();
            
            ProcessNode(scene->MRootNode, scene, vertices, indices);
            
            _assimp.FreeScene(scene);
            
            // Transform coordinates if needed
            if (detectedUpAxis != TargetUpAxis && detectedUpAxis != UpAxis.Unknown)
            {
                TransformVertices(vertices, detectedUpAxis, TargetUpAxis);
            }
            
            // Calculate bounding box
            var positions = new List<Vector3>();
            foreach (var v in vertices)
            {
                positions.Add(v.Position);
            }
            
            return new MeshData
            {
                Vertices = vertices.ToArray(),
                Indices = indices.ToArray(),
                Bounds = BoundingBox.CreateFromPoints(positions),
                OriginalUpAxis = detectedUpAxis
            };
        }
        
        /// <summary>
        /// Detect up-axis from file extension and metadata
        /// </summary>
        private UpAxis DetectUpAxis(string filePath, Scene* scene)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            
            // Common defaults by format:
            // FBX: Y-up (Maya default) or Z-up (3ds Max default)
            // OBJ: Y-up (most common)
            // GLTF/GLB: Y-up (specification)
            // Blender exports: Z-up
            // 3ds/3DS Max: Z-up
            // DAE/Collada: Y-up
            
            switch (ext)
            {
                case ".gltf":
                case ".glb":
                case ".obj":
                case ".dae":
                    return UpAxis.YUp;
                    
                case ".3ds":
                case ".stl":
                case ".ply":
                    return UpAxis.ZUp;
                    
                case ".fbx":
                    // FBX can be either - try to detect from scene metadata
                    // or use heuristics based on bounding box aspect ratio
                    return DetectFbxUpAxis(scene);
                    
                case ".blend":
                    return UpAxis.ZUp; // Blender native is Z-up
                    
                default:
                    return UpAxis.Unknown;
            }
        }
        
        /// <summary>
        /// Try to detect FBX up-axis from scene data
        /// </summary>
        private UpAxis DetectFbxUpAxis(Scene* scene)
        {
            // Check if the scene has metadata
            if (scene->MMetaData != null)
            {
                var metadata = scene->MMetaData;
                
                // Look for UpAxis key (FBX stores this)
                for (uint i = 0; i < metadata->MNumProperties; i++)
                {
                    var key = metadata->MKeys[i];
                    var keyStr = key.AsString;
                    
                    if (keyStr == "UpAxis" || keyStr == "OriginalUpAxis")
                    {
                        var value = metadata->MValues[i];
                        if (value.MType == MetadataType.Int32)
                        {
                            // FBX: 0=X, 1=Y, 2=Z
                            var axisValue = *(int*)value.MData;
                            return axisValue == 2 ? UpAxis.ZUp : UpAxis.YUp;
                        }
                    }
                }
            }
            
            // Default FBX to Y-up (Maya export default)
            return UpAxis.YUp;
        }
        
        /// <summary>
        /// Transform vertices from source to target up-axis
        /// </summary>
        private void TransformVertices(List<Vertex> vertices, UpAxis source, UpAxis target)
        {
            Matrix4x4 transform;
            
            if (source == UpAxis.YUp && target == UpAxis.ZUp)
            {
                // Y-up to Z-up: rotate -90° around X
                // (x, y, z) -> (x, -z, y)
                transform = Matrix4x4.CreateRotationX(-MathF.PI / 2f);
            }
            else if (source == UpAxis.ZUp && target == UpAxis.YUp)
            {
                // Z-up to Y-up: rotate 90° around X
                // (x, y, z) -> (x, z, -y)
                transform = Matrix4x4.CreateRotationX(MathF.PI / 2f);
            }
            else
            {
                return; // No transform needed
            }
            
            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                v.Position = Vector3.Transform(v.Position, transform);
                v.Normal = Vector3.TransformNormal(v.Normal, transform);
                vertices[i] = v;
            }
        }
        
        private void ProcessNode(Node* node, Scene* scene, List<Vertex> vertices, List<uint> indices)
        {
            // Process all meshes in this node
            for (uint i = 0; i < node->MNumMeshes; i++)
            {
                var mesh = scene->MMeshes[node->MMeshes[i]];
                ProcessMesh(mesh, scene, vertices, indices);
            }
            
            // Recursively process child nodes
            for (uint i = 0; i < node->MNumChildren; i++)
            {
                ProcessNode(node->MChildren[i], scene, vertices, indices);
            }
        }
        
        private void ProcessMesh(Mesh* mesh, Scene* scene, List<Vertex> vertices, List<uint> indices)
        {
            uint baseVertex = (uint)vertices.Count;
            
            // Process vertices
            for (uint i = 0; i < mesh->MNumVertices; i++)
            {
                var vertex = new Vertex();
                
                // Position
                vertex.Position = new Vector3(
                    mesh->MVertices[i].X,
                    mesh->MVertices[i].Y,
                    mesh->MVertices[i].Z
                );
                
                // Normal
                if (mesh->MNormals != null)
                {
                    vertex.Normal = new Vector3(
                        mesh->MNormals[i].X,
                        mesh->MNormals[i].Y,
                        mesh->MNormals[i].Z
                    );
                }
                else
                {
                    vertex.Normal = Vector3.UnitZ;
                }
                
                // Texture Coordinates (use first set if available)
                if (mesh->MTextureCoords[0] != null)
                {
                    vertex.TexCoord = new Vector2(
                        mesh->MTextureCoords[0][i].X,
                        mesh->MTextureCoords[0][i].Y
                    );
                }
                else
                {
                    vertex.TexCoord = Vector2.Zero;
                }
                
                vertices.Add(vertex);
            }
            
            // Process indices
            for (uint i = 0; i < mesh->MNumFaces; i++)
            {
                var face = mesh->MFaces[i];
                for (uint j = 0; j < face.MNumIndices; j++)
                {
                    indices.Add(baseVertex + face.MIndices[j]);
                }
            }
        }
        
        public void Dispose()
        {
            _assimp?.Dispose();
            _assimp = null;
        }
    }
    
    /// <summary>
    /// Contains mesh vertex and index data
    /// </summary>
    public class MeshData
    {
        public Vertex[] Vertices { get; set; } = Array.Empty<Vertex>();
        public uint[] Indices { get; set; } = Array.Empty<uint>();
        public BoundingBox Bounds { get; set; }
        public UpAxis OriginalUpAxis { get; set; } = UpAxis.Unknown;
    }
}

