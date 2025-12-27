using System;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace Pivot.Utilities
{
    /// <summary>
    /// Manages mesh data including vertex and index buffers, and model loading.
    /// </summary>
    public unsafe class MeshManager : IDisposable
    {
        private bool _disposed;
        private readonly VulkanCore _core;

        // Vertex and Index Buffers
        private Silk.NET.Vulkan.Buffer _vkVertexBuffer;
        private DeviceMemory _vkVertexBufferMemory;
        private Silk.NET.Vulkan.Buffer _vkIndexBuffer;
        private DeviceMemory _vkIndexBufferMemory;
        private uint _indexCount = 3; // Default for initial triangle

        // Public accessors
        public Silk.NET.Vulkan.Buffer VertexBuffer => _vkVertexBuffer;
        public Silk.NET.Vulkan.Buffer IndexBuffer => _vkIndexBuffer;
        public uint IndexCount => _indexCount;

        public MeshManager(VulkanCore core)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));
        }

        /// <summary>
        /// Create default vertex buffer (initial triangle)
        /// </summary>
        public void CreateDefaultMesh()
        {
            var vertices = new[]
            {
                new Vertex { Position = new System.Numerics.Vector3(-0.5f, -0.5f, 0.0f), Normal = new System.Numerics.Vector3(0.0f, 0.0f, 1.0f), TexCoord = new System.Numerics.Vector2(1.0f, 0.0f) },
                new Vertex { Position = new System.Numerics.Vector3( 0.5f, -0.5f, 0.0f), Normal = new System.Numerics.Vector3(0.0f, 0.0f, 1.0f), TexCoord = new System.Numerics.Vector2(0.0f, 0.0f) },
                new Vertex { Position = new System.Numerics.Vector3( 0.5f,  0.5f, 0.0f), Normal = new System.Numerics.Vector3(0.0f, 0.0f, 1.0f), TexCoord = new System.Numerics.Vector2(0.0f, 1.0f) },
            };
            var indices = new uint[] { 0, 1, 2 };

            UploadMeshData(vertices, indices);
        }

        /// <summary>
        /// Load a 3D model from file
        /// </summary>
        public void LoadModel(string filePath)
        {
            var vk = _core.Vk;
            var device = _core.Device;

            vk.DeviceWaitIdle(device);

            // Cleanup old buffers
            CleanupBuffers();

            using var loader = new ModelLoader();
            var meshData = loader.LoadModel(filePath);

            if (meshData != null)
            {
                UploadMeshData(meshData.Vertices, meshData.Indices);
            }
        }

        /// <summary>
        /// Upload mesh data to GPU (must be called from UI thread)
        /// </summary>
        public void UploadMesh(Vertex[] vertices, uint[] indices)
        {
            if (_disposed) return;

            var vk = _core.Vk;
            var device = _core.Device;

            // Wait for GPU to finish all operations
            vk.DeviceWaitIdle(device);

            // Cleanup old buffers
            CleanupBuffers();

            UploadMeshData(vertices, indices);
        }

        private void UploadMeshData(Vertex[] vertices, uint[] indices)
        {
            var vk = _core.Vk;
            var device = _core.Device;

            if (vertices.Length == 0 || indices.Length == 0)
            {
                _indexCount = 0;
                // Leave buffers as null (CleanupBuffers called before this)
                return;
            }

            _indexCount = (uint)indices.Length;

            // Vertex Buffer
            ulong vertexBufferSize = (ulong)(vertices.Length * Marshal.SizeOf<Vertex>());
            CreateBuffer(vertexBufferSize, BufferUsageFlags.VertexBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out _vkVertexBuffer, out _vkVertexBufferMemory);

            void* vertexData;
            VulkanCore.CheckVkResult(vk.MapMemory(device, _vkVertexBufferMemory, 0, vertexBufferSize, 0, &vertexData));
            fixed (Vertex* ptr = vertices)
            {
                System.Buffer.MemoryCopy(ptr, vertexData, vertexBufferSize, vertexBufferSize);
            }
            vk.UnmapMemory(device, _vkVertexBufferMemory);

            // Index Buffer
            ulong indexBufferSize = (ulong)(indices.Length * sizeof(uint));
            CreateBuffer(indexBufferSize, BufferUsageFlags.IndexBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out _vkIndexBuffer, out _vkIndexBufferMemory);

            void* indexData;
            VulkanCore.CheckVkResult(vk.MapMemory(device, _vkIndexBufferMemory, 0, indexBufferSize, 0, &indexData));
            fixed (uint* ptr = indices)
            {
                System.Buffer.MemoryCopy(ptr, indexData, indexBufferSize, indexBufferSize);
            }
            vk.UnmapMemory(device, _vkIndexBufferMemory);
        }

        /// <summary>
        /// Create a Vulkan buffer with specified usage and memory properties
        /// </summary>
        public void CreateBuffer(ulong size, BufferUsageFlags usage, MemoryPropertyFlags properties, out Silk.NET.Vulkan.Buffer buffer, out DeviceMemory bufferMemory)
        {
            var vk = _core.Vk;
            var device = _core.Device;

            var bufferInfo = new BufferCreateInfo
            {
                SType = StructureType.BufferCreateInfo,
                Size = size,
                Usage = usage,
                SharingMode = SharingMode.Exclusive
            };

            VulkanCore.CheckVkResult(vk.CreateBuffer(device, in bufferInfo, null, out buffer));

            vk.GetBufferMemoryRequirements(device, buffer, out var memRequirements);

            var allocInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memRequirements.Size,
                MemoryTypeIndex = _core.FindMemoryType(memRequirements.MemoryTypeBits, properties)
            };

            VulkanCore.CheckVkResult(vk.AllocateMemory(device, in allocInfo, null, out bufferMemory));
            VulkanCore.CheckVkResult(vk.BindBufferMemory(device, buffer, bufferMemory, 0));
        }

        private void CleanupBuffers()
        {
            var vk = _core.Vk;
            var device = _core.Device;

            if (_vkVertexBuffer.Handle != 0)
            {
                vk.DestroyBuffer(device, _vkVertexBuffer, null);
                vk.FreeMemory(device, _vkVertexBufferMemory, null);
            }
            if (_vkIndexBuffer.Handle != 0)
            {
                vk.DestroyBuffer(device, _vkIndexBuffer, null);
                vk.FreeMemory(device, _vkIndexBufferMemory, null);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            CleanupBuffers();

            _disposed = true;
        }
    }
}
