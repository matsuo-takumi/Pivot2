using System;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Pivot.Models;

namespace Pivot.Utilities
{
    /// <summary>
    /// Main Vulkan renderer that integrates all modules for 3D rendering.
    /// Uses VulkanCore, SwapChainManager, PipelineManager, and MeshManager.
    /// </summary>
    public unsafe class VulkanInteropRenderer : IDisposable
    {
        private bool _disposed;

        // Modular components
        private readonly VulkanCore _core;
        private readonly SwapChainManager _swapChainManager;
        private readonly PipelineManager _pipelineManager;
        private readonly MeshManager _meshManager;

        // Command resources (kept in main renderer)
        private CommandPool _vkCommandPool;
        private CommandBuffer _vkCommandBuffer;
        private Fence _vkFence;

        // Depth Buffer
        private Image _vkDepthImage;
        private DeviceMemory _vkDepthImageMemory;
        private ImageView _vkDepthImageView;

        // Uniform Buffers
        private Silk.NET.Vulkan.Buffer _vkUniformBuffer;
        private DeviceMemory _vkUniformBufferMemory;
        private void* _vkUniformBufferMapped;

        // Shading Buffer
        private Silk.NET.Vulkan.Buffer _vkShadingBuffer;
        private DeviceMemory _vkShadingBufferMemory;
        private void* _vkShadingBufferMapped;

        // Current dimensions
        private int _currentWidth;
        private int _currentHeight;

        // Shading Mode
        private ShadingMode _currentShadingMode = ShadingMode.WorldNormal;

        // Background color (RGBA)
        private float _bgColorR = 0.2f;
        private float _bgColorG = 0.6f;
        private float _bgColorB = 0.8f;
        private float _bgColorA = 1.0f; // 0.0 = transparent (shows Mica), 1.0 = opaque

        // Light parameters
        private System.Numerics.Vector3 _lightDirection = System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(0.5f, 1.0f, 0.3f));
        private float _lightIntensity = 1.0f;
        private System.Numerics.Vector3 _lightColor = new System.Numerics.Vector3(1f, 1f, 1f);

        // Material parameters (PBR)
        private System.Numerics.Vector3 _materialAlbedo = new System.Numerics.Vector3(0.7f, 0.7f, 0.7f);
        private float _materialMetallic = 0.0f;
        private float _materialRoughness = 0.5f;

        // Shader toggle flags
        private bool _useTexture = false;
        private bool _useVertexColor = false;
        private bool _useUVChecker = false;
        private bool _useMaterial = true;

        [StructLayout(LayoutKind.Sequential)]
        private struct ShadingParams
        {
            public int Mode;
            public float NearPlane;
            public float FarPlane;
            public float LightIntensity;
            public System.Numerics.Vector3 LightDirection;
            public float _padding1;
            public System.Numerics.Vector3 LightColor;
            public float _padding2;
            // PBR Material params
            public System.Numerics.Vector3 MaterialAlbedo;
            public float MaterialMetallic;
            public float MaterialRoughness;
            // Shader toggles (as int for GLSL compatibility)
            public int UseTexture;
            public int UseVertexColor;
            public int UseUVChecker;
            public int UseMaterial;
        }

        public VulkanInteropRenderer()
        {
            _core = new VulkanCore();
            _swapChainManager = new SwapChainManager(_core);
            _pipelineManager = new PipelineManager(_core);
            _meshManager = new MeshManager(_core);
        }

        public void SetBackfaceCulling(bool enabled)
        {
            _pipelineManager.SetBackfaceCulling(enabled);
        }

        public void Initialize(Microsoft.UI.Xaml.Controls.SwapChainPanel panel, int width, int height)
        {
            _currentWidth = width;
            _currentHeight = height;

            // Initialize core Vulkan
            _core.Initialize();

            // Initialize DirectX and SwapChain
            _swapChainManager.InitializeDirectX(panel, width, height);
            _swapChainManager.ImportSharedTexture(width, height);

            // Create uniform buffers (needed before descriptor sets)
            CreateUniformBuffers();

            // Initialize pipeline with descriptors
            _pipelineManager.CreateDescriptorSetLayout();
            _pipelineManager.CreateDescriptorPool();
            _pipelineManager.CreateDescriptorSets(
                _vkUniformBuffer, (ulong)Marshal.SizeOf<UniformBufferObject>(),
                _vkShadingBuffer, (ulong)Marshal.SizeOf<ShadingParams>());

            // Create depth resources
            CreateDepthResources();

            // Initialize pipeline with render pass and framebuffer
            _pipelineManager.CreateRenderPass();
            _pipelineManager.CreatePipeline();
            _pipelineManager.CreateFramebuffer(_swapChainManager.VkImageView, _vkDepthImageView);

            // Create mesh
            _meshManager.CreateDefaultMesh();

            // Create command resources
            CreateCommandBuffers();
            CreateSyncObjects();
        }

        public void LoadModel(string filePath)
        {
            _meshManager.LoadModel(filePath);
        }

        public void UploadMesh(Vertex[] vertices, uint[] indices)
        {
            _meshManager.UploadMesh(vertices, indices);
        }

        public void Render(OrbitCamera camera)
        {
            if (_disposed) return;

            var vk = _core.Vk;
            var device = _core.Device;

            // 1. Wait for Fence
            vk.WaitForFences(device, 1, in _vkFence, true, ulong.MaxValue);
            vk.ResetFences(device, 1, in _vkFence);

            // 2. Update UBO
            UpdateUniformBuffer((float)_currentWidth / _currentHeight, camera);
            UpdateShadingBuffer();

            // 3. Record Command Buffer
            vk.ResetCommandBuffer(_vkCommandBuffer, 0);

            var beginInfo = new CommandBufferBeginInfo
            {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.OneTimeSubmitBit
            };

            VulkanCore.CheckVkResult(vk.BeginCommandBuffer(_vkCommandBuffer, in beginInfo));

            var clearValues = stackalloc ClearValue[2];
            clearValues[0].Color = new ClearColorValue { Float32_0 = _bgColorR, Float32_1 = _bgColorG, Float32_2 = _bgColorB, Float32_3 = _bgColorA };
            clearValues[1].DepthStencil = new ClearDepthStencilValue { Depth = 1.0f, Stencil = 0 };

            var renderPassInfo = new RenderPassBeginInfo
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = _pipelineManager.RenderPass,
                Framebuffer = _pipelineManager.Framebuffer,
                RenderArea =
                {
                    Offset = { X = 0, Y = 0 },
                    Extent = { Width = (uint)_currentWidth, Height = (uint)_currentHeight }
                },
                ClearValueCount = 2,
                PClearValues = clearValues
            };

            vk.CmdBeginRenderPass(_vkCommandBuffer, in renderPassInfo, SubpassContents.Inline);

            // Bind the graphics pipeline
            vk.CmdBindPipeline(_vkCommandBuffer, PipelineBindPoint.Graphics, _pipelineManager.Pipeline);

            // Set dynamic viewport and scissor
            var viewport = new Viewport(0, 0, _currentWidth, _currentHeight, 0, 1);
            vk.CmdSetViewport(_vkCommandBuffer, 0, 1, in viewport);

            var scissor = new Rect2D(new Offset2D(0, 0), new Extent2D((uint)_currentWidth, (uint)_currentHeight));
            vk.CmdSetScissor(_vkCommandBuffer, 0, 1, in scissor);

            // Bind descriptor sets
            var descriptorSet = _pipelineManager.DescriptorSet;
            vk.CmdBindDescriptorSets(_vkCommandBuffer, PipelineBindPoint.Graphics, _pipelineManager.PipelineLayout, 0, 1, &descriptorSet, 0, null);

            // Bind vertex buffer
            var offset = 0ul;
            var vertexBuffer = _meshManager.VertexBuffer;
            vk.CmdBindVertexBuffers(_vkCommandBuffer, 0, 1, in vertexBuffer, in offset);

            // Bind index buffer
            vk.CmdBindIndexBuffer(_vkCommandBuffer, _meshManager.IndexBuffer, 0, IndexType.Uint32);

            // Draw indexed
            vk.CmdDrawIndexed(_vkCommandBuffer, _meshManager.IndexCount, 1, 0, 0, 0);

            vk.CmdEndRenderPass(_vkCommandBuffer);
            VulkanCore.CheckVkResult(vk.EndCommandBuffer(_vkCommandBuffer));

            // 4. Submit with Keyed Mutex
            var acquireKeys = stackalloc ulong[1];
            var acquireTimeouts = stackalloc uint[1];
            var releaseKeys = stackalloc ulong[1];

            acquireKeys[0] = 0;
            acquireTimeouts[0] = uint.MaxValue;
            releaseKeys[0] = 1;

            var deviceMemory = _swapChainManager.VkDeviceMemory;
            var mutexInfo = new Win32KeyedMutexAcquireReleaseInfoKHR
            {
                SType = StructureType.Win32KeyedMutexAcquireReleaseInfoKhr,
                AcquireCount = 1,
                PAcquireSyncs = &deviceMemory,
                PAcquireKeys = acquireKeys,
                PAcquireTimeouts = acquireTimeouts,
                ReleaseCount = 1,
                PReleaseSyncs = &deviceMemory,
                PReleaseKeys = releaseKeys
            };

            fixed (CommandBuffer* pCommandBuffer = &_vkCommandBuffer)
            {
                var submitInfo = new SubmitInfo
                {
                    SType = StructureType.SubmitInfo,
                    CommandBufferCount = 1,
                    PCommandBuffers = pCommandBuffer,
                    PNext = &mutexInfo
                };

                VulkanCore.CheckVkResult(vk.QueueSubmit(_core.GraphicsQueue, 1, in submitInfo, _vkFence));
            }

            // 5. Present
            _swapChainManager.Present();
        }

        public void Resize(int width, int height)
        {
            if (_disposed) return;

            _core.WaitIdle();

            // Dispose old depth resources
            var vk = _core.Vk;
            var device = _core.Device;
            vk.DestroyImageView(device, _vkDepthImageView, null);
            vk.FreeMemory(device, _vkDepthImageMemory, null);
            vk.DestroyImage(device, _vkDepthImage, null);

            // Update dimensions
            _currentWidth = width;
            _currentHeight = height;

            // Resize swap chain
            _swapChainManager.Resize(width, height);

            // Recreate depth buffer
            CreateDepthResources();

            // Update pipeline framebuffer
            _pipelineManager.UpdateSize(width, height, _swapChainManager.VkImageView, _vkDepthImageView);
        }

        #region Settings Methods

        public void SetShadingMode(ShadingMode mode)
        {
            _currentShadingMode = mode;
        }

        public void SetBackgroundColor(float r, float g, float b)
        {
            _bgColorR = r;
            _bgColorG = g;
            _bgColorB = b;
            _bgColorA = 1.0f;
        }

        public void SetBackgroundColor(float r, float g, float b, float a)
        {
            _bgColorR = r;
            _bgColorG = g;
            _bgColorB = b;
            _bgColorA = a;
        }

        /// <summary>
        /// Set transparent background to show Mica backdrop
        /// </summary>
        public void SetTransparentBackground()
        {
            _bgColorR = 0.0f;
            _bgColorG = 0.0f;
            _bgColorB = 0.0f;
            _bgColorA = 0.0f;
        }

        public void SetLightParams(System.Numerics.Vector3 direction, float intensity, System.Numerics.Vector3 color)
        {
            _lightDirection = System.Numerics.Vector3.Normalize(direction);
            _lightIntensity = intensity;
            _lightColor = color;
        }

        /// <summary>
        /// Set PBR material parameters
        /// </summary>
        public void SetMaterialParams(float r, float g, float b, float metallic, float roughness)
        {
            _materialAlbedo = new System.Numerics.Vector3(r, g, b);
            _materialMetallic = metallic;
            _materialRoughness = roughness;
        }

        /// <summary>
        /// Set shader toggle states for Material-based rendering
        /// </summary>
        public void SetShaderToggles(bool useTexture, bool useVertexColor, bool useUVChecker, bool useMaterial)
        {
            _useTexture = useTexture;
            _useVertexColor = useVertexColor;
            _useUVChecker = useUVChecker;
            _useMaterial = useMaterial;
        }

        #endregion

        #region Private Methods

        private void CreateDepthResources()
        {
            var vk = _core.Vk;
            var device = _core.Device;

            var depthFormat = FindDepthFormat();

            CreateImage((uint)_currentWidth, (uint)_currentHeight, depthFormat, ImageTiling.Optimal,
                ImageUsageFlags.DepthStencilAttachmentBit, MemoryPropertyFlags.DeviceLocalBit,
                out _vkDepthImage, out _vkDepthImageMemory);

            _vkDepthImageView = CreateImageView(_vkDepthImage, depthFormat, ImageAspectFlags.DepthBit);
        }

        private Silk.NET.Vulkan.Format FindDepthFormat()
        {
            return FindSupportedFormat(
                new[] { Silk.NET.Vulkan.Format.D32Sfloat, Silk.NET.Vulkan.Format.D32SfloatS8Uint, Silk.NET.Vulkan.Format.D24UnormS8Uint },
                ImageTiling.Optimal,
                FormatFeatureFlags.DepthStencilAttachmentBit
            );
        }

        private Silk.NET.Vulkan.Format FindSupportedFormat(Silk.NET.Vulkan.Format[] candidates, ImageTiling tiling, FormatFeatureFlags features)
        {
            var vk = _core.Vk;

            foreach (var format in candidates)
            {
                vk.GetPhysicalDeviceFormatProperties(_core.PhysicalDevice, format, out var props);

                if (tiling == ImageTiling.Linear && (props.LinearTilingFeatures & features) == features)
                {
                    return format;
                }
                else if (tiling == ImageTiling.Optimal && (props.OptimalTilingFeatures & features) == features)
                {
                    return format;
                }
            }

            throw new Exception("Failed to find supported format!");
        }

        private void CreateImage(uint width, uint height, Silk.NET.Vulkan.Format format, ImageTiling tiling, ImageUsageFlags usage, MemoryPropertyFlags properties, out Image image, out DeviceMemory imageMemory)
        {
            var vk = _core.Vk;
            var device = _core.Device;

            var imageInfo = new ImageCreateInfo
            {
                SType = StructureType.ImageCreateInfo,
                ImageType = ImageType.Type2D,
                Extent = new Extent3D { Width = width, Height = height, Depth = 1 },
                MipLevels = 1,
                ArrayLayers = 1,
                Format = format,
                Tiling = tiling,
                InitialLayout = ImageLayout.Undefined,
                Usage = usage,
                SharingMode = SharingMode.Exclusive,
                Samples = SampleCountFlags.Count1Bit,
                Flags = 0
            };

            VulkanCore.CheckVkResult(vk.CreateImage(device, in imageInfo, null, out image));

            vk.GetImageMemoryRequirements(device, image, out var memRequirements);

            var allocInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memRequirements.Size,
                MemoryTypeIndex = _core.FindMemoryType(memRequirements.MemoryTypeBits, properties)
            };

            VulkanCore.CheckVkResult(vk.AllocateMemory(device, in allocInfo, null, out imageMemory));
            VulkanCore.CheckVkResult(vk.BindImageMemory(device, image, imageMemory, 0));
        }

        private ImageView CreateImageView(Image image, Silk.NET.Vulkan.Format format, ImageAspectFlags aspectFlags)
        {
            var vk = _core.Vk;
            var device = _core.Device;

            var createInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = image,
                ViewType = ImageViewType.Type2D,
                Format = format,
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = aspectFlags,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };
            VulkanCore.CheckVkResult(vk.CreateImageView(device, in createInfo, null, out var imageView));
            return imageView;
        }

        private void CreateUniformBuffers()
        {
            var vk = _core.Vk;
            var device = _core.Device;

            // 1. Camera UBO
            ulong bufferSize = (ulong)Marshal.SizeOf<UniformBufferObject>();
            _meshManager.CreateBuffer(bufferSize, BufferUsageFlags.UniformBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out _vkUniformBuffer, out _vkUniformBufferMemory);

            void* pMapped;
            VulkanCore.CheckVkResult(vk.MapMemory(device, _vkUniformBufferMemory, 0, bufferSize, 0, &pMapped));
            _vkUniformBufferMapped = pMapped;

            // 2. Shading Params UBO
            ulong shadingBufferSize = (ulong)Marshal.SizeOf<ShadingParams>();
            _meshManager.CreateBuffer(shadingBufferSize, BufferUsageFlags.UniformBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out _vkShadingBuffer, out _vkShadingBufferMemory);

            void* pShadingMapped;
            VulkanCore.CheckVkResult(vk.MapMemory(device, _vkShadingBufferMemory, 0, shadingBufferSize, 0, &pShadingMapped));
            _vkShadingBufferMapped = pShadingMapped;
        }

        private void UpdateUniformBuffer(float aspectRatio, OrbitCamera camera)
        {
            // Calculate far plane dynamically based on camera distance
            float dist = camera.Distance;
            float farPlane = (float)Math.Max(dist * 10.0, 10000.0);

            // Flip X and Z to correct coordinate system for Vulkan
            var modelMatrix = System.Numerics.Matrix4x4.CreateScale(-1f, 1f, -1f);

            var ubo = new UniformBufferObject
            {
                Model = modelMatrix,
                View = camera.GetViewMatrix(),
                Proj = System.Numerics.Matrix4x4.CreatePerspectiveFieldOfView((float)Math.PI / 4.0f, aspectRatio, 0.1f, farPlane)
            };

            // Vulkan's Y coordinate is inverted comparing to OpenGL
            ubo.Proj.M22 *= -1;

            System.Buffer.MemoryCopy(&ubo, _vkUniformBufferMapped, (ulong)Marshal.SizeOf<UniformBufferObject>(), (ulong)Marshal.SizeOf<UniformBufferObject>());
        }

        private void UpdateShadingBuffer()
        {
            var shadingParams = new ShadingParams
            {
                Mode = (int)_currentShadingMode,
                NearPlane = 0.1f,
                FarPlane = 100.0f,
                LightIntensity = _lightIntensity,
                LightDirection = _lightDirection,
                _padding1 = 0.0f,
                LightColor = _lightColor,
                _padding2 = 0.0f,
                // PBR Material params
                MaterialAlbedo = _materialAlbedo,
                MaterialMetallic = _materialMetallic,
                MaterialRoughness = _materialRoughness,
                // Shader toggles
                UseTexture = _useTexture ? 1 : 0,
                UseVertexColor = _useVertexColor ? 1 : 0,
                UseUVChecker = _useUVChecker ? 1 : 0,
                UseMaterial = _useMaterial ? 1 : 0
            };

            System.Buffer.MemoryCopy(&shadingParams, _vkShadingBufferMapped, (ulong)Marshal.SizeOf<ShadingParams>(), (ulong)Marshal.SizeOf<ShadingParams>());
        }

        private void CreateCommandBuffers()
        {
            var vk = _core.Vk;
            var device = _core.Device;

            var poolInfo = new CommandPoolCreateInfo
            {
                SType = StructureType.CommandPoolCreateInfo,
                QueueFamilyIndex = 0,
                Flags = CommandPoolCreateFlags.ResetCommandBufferBit
            };

            VulkanCore.CheckVkResult(vk.CreateCommandPool(device, in poolInfo, null, out _vkCommandPool));

            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = _vkCommandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = 1
            };

            VulkanCore.CheckVkResult(vk.AllocateCommandBuffers(device, in allocInfo, out _vkCommandBuffer));
        }

        private void CreateSyncObjects()
        {
            var vk = _core.Vk;
            var device = _core.Device;

            var fenceInfo = new FenceCreateInfo
            {
                SType = StructureType.FenceCreateInfo,
                Flags = FenceCreateFlags.SignaledBit
            };

            VulkanCore.CheckVkResult(vk.CreateFence(device, in fenceInfo, null, out _vkFence));
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;

            _core.WaitIdle();

            var vk = _core.Vk;
            var device = _core.Device;

            // Uniform buffers
            vk.DestroyBuffer(device, _vkUniformBuffer, null);
            vk.FreeMemory(device, _vkUniformBufferMemory, null);
            vk.DestroyBuffer(device, _vkShadingBuffer, null);
            vk.FreeMemory(device, _vkShadingBufferMemory, null);

            // Depth Resources
            vk.DestroyImageView(device, _vkDepthImageView, null);
            vk.FreeMemory(device, _vkDepthImageMemory, null);
            vk.DestroyImage(device, _vkDepthImage, null);

            // Command resources
            vk.DestroyFence(device, _vkFence, null);
            vk.DestroyCommandPool(device, _vkCommandPool, null);

            // Dispose modules in reverse order
            _meshManager.Dispose();
            _pipelineManager.Dispose();
            _swapChainManager.Dispose();
            _core.Dispose();

            _disposed = true;
        }
    }
}
