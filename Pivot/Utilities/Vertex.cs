using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using VkFormat = Silk.NET.Vulkan.Format;

namespace Pivot.Utilities
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TexCoord;
        public Vector4 Color; // RGBA vertex color

        public static VertexInputBindingDescription GetBindingDescription()
        {
            return new VertexInputBindingDescription
            {
                Binding = 0,
                Stride = (uint)Marshal.SizeOf<Vertex>(),
                InputRate = VertexInputRate.Vertex
            };
        }

        public static VertexInputAttributeDescription[] GetAttributeDescriptions()
        {
            return new[]
            {
                new VertexInputAttributeDescription
                {
                    Binding = 0,
                    Location = 0,
                    Format = VkFormat.R32G32B32Sfloat,
                    Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Position))
                },
                new VertexInputAttributeDescription
                {
                    Binding = 0,
                    Location = 1,
                    Format = VkFormat.R32G32B32Sfloat,
                    Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Normal))
                },
                new VertexInputAttributeDescription
                {
                    Binding = 0,
                    Location = 2,
                    Format = VkFormat.R32G32Sfloat,
                    Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(TexCoord))
                },
                new VertexInputAttributeDescription
                {
                    Binding = 0,
                    Location = 3,
                    Format = VkFormat.R32G32B32A32Sfloat,
                    Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Color))
                }
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UniformBufferObject
    {
        public Matrix4x4 Model;
        public Matrix4x4 View;
        public Matrix4x4 Proj;
    }
}
