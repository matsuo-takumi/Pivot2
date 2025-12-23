using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Vortice.Direct3D11;
using Vortice.DXGI;
using DxgiFormat = Vortice.DXGI.Format;
using VkFormat = Silk.NET.Vulkan.Format;
using DxgiSharedResourceFlags = Vortice.DXGI.SharedResourceFlags;
using System.IO;
using Pivot.Models;

namespace Pivot.Utilities
{
    /// <summary>
    /// Custom ISwapChainPanelNative COM interface for WinUI 3 interop
    /// </summary>
    [ComImport]
    [Guid("63aad0b8-7c24-40ff-85a8-640d944cc325")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISwapChainPanelNative
    {
        void SetSwapChain(IntPtr swapChain);  // Use IntPtr to avoid ComVisible issues with Vortice wrappers
    }

    public unsafe class VulkanInteropRenderer : IDisposable
    {
        private bool _disposed;

        // DirectX 11
        private ID3D11Device? _d3dDevice;
        private ID3D11DeviceContext? _d3dContext;
        private IDXGISwapChain1? _swapChain;
        private ID3D11Texture2D? _sharedTexture;
        private IntPtr _sharedHandle = IntPtr.Zero;

        // Vulkan
        private Vk _vk;
        private Instance _instance;
        private PhysicalDevice _physicalDevice;
        private Device _device;
        private Queue _graphicsQueue;
        
        // Vulkan Extensions
        private KhrExternalMemoryWin32? _khrExternalMemoryWin32;
        private KhrWin32Surface? _khrWin32Surface;

        // Vulkan Resources
        private Image _vkImage;
        private DeviceMemory _vkDeviceMemory;
        private ImageView _vkImageView;
        private Framebuffer _vkFramebuffer;
        private RenderPass _vkRenderPass;
        private CommandPool _vkCommandPool;
        private CommandBuffer _vkCommandBuffer;
        private Fence _vkFence;
        
        // Pipeline Resources
        private PipelineLayout _vkPipelineLayout;
        private Pipeline _vkPipeline;
        private ShaderModule _vkVertShaderModule;
        private ShaderModule _vkFragShaderModule;
        
        // Vertex Buffer
        private Silk.NET.Vulkan.Buffer _vkVertexBuffer;
        private DeviceMemory _vkVertexBufferMemory;
        private Silk.NET.Vulkan.Buffer _vkIndexBuffer;
        private DeviceMemory _vkIndexBufferMemory;

        // Depth Buffer
        private Image _vkDepthImage;
        private DeviceMemory _vkDepthImageMemory;
        private ImageView _vkDepthImageView;

        // Uniform Buffers
        private Silk.NET.Vulkan.Buffer _vkUniformBuffer;
        private DeviceMemory _vkUniformBufferMemory;
        private void* _vkUniformBufferMapped;

        // Descriptors
        private DescriptorSetLayout _vkDescriptorSetLayout;
        private DescriptorPool _vkDescriptorPool;
        private DescriptorSet _vkDescriptorSet;
        
        private int _currentWidth;
        private int _currentHeight;
        
        // Rendering settings
        private bool _backfaceCulling = true;

        public VulkanInteropRenderer()
        {
            _vk = Vk.GetApi();
        }

        public void SetBackfaceCulling(bool enabled)
        {
            if (_backfaceCulling == enabled) return;
            _backfaceCulling = enabled;
            
            // Recreate pipeline with new culling mode
            if (_vkPipeline.Handle != 0)
            {
                _vk.DeviceWaitIdle(_device);
                _vk.DestroyPipeline(_device, _vkPipeline, null);
                _vk.DestroyPipelineLayout(_device, _vkPipelineLayout, null);
                CreatePipeline();
            }
        }

        public void Initialize(Microsoft.UI.Xaml.Controls.SwapChainPanel panel, int width, int height)
        {
            _currentWidth = width;
            _currentHeight = height;

            InitializeDirectX(panel, width, height);
            InitializeVulkan();
            ImportSharedTexture(width, height);
            
            CreateDescriptorSetLayout();
            CreateUniformBuffers();
            CreateDescriptorPool();
            CreateDescriptorSets();
            
            CreateRenderPass();
            CreatePipeline();
            
            CreateDepthResources();
            CreateFramebuffer(width, height);
            
            CreateVertexBuffer();
            
            CreateCommandBuffers();
            CreateSyncObjects();
        }

        // ... InitializeDirectX ...
        // ... InitializeVulkan ...
        // ... ImportSharedTexture ...

        private void CreateRenderPass()
        {
            var colorAttachment = new AttachmentDescription
            {
                Format = VkFormat.B8G8R8A8Unorm,
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.PresentSrcKhr
            };

            var colorAttachmentRef = new AttachmentReference
            {
                Attachment = 0,
                Layout = ImageLayout.ColorAttachmentOptimal
            };

            var depthAttachment = new AttachmentDescription
            {
                Format = FindDepthFormat(),
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.DontCare,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.DepthStencilAttachmentOptimal
            };

            var depthAttachmentRef = new AttachmentReference
            {
                Attachment = 1,
                Layout = ImageLayout.DepthStencilAttachmentOptimal
            };

            var subpass = new SubpassDescription
            {
                PipelineBindPoint = PipelineBindPoint.Graphics,
                ColorAttachmentCount = 1,
                PColorAttachments = &colorAttachmentRef,
                PDepthStencilAttachment = &depthAttachmentRef
            };

            var dependency = new SubpassDependency
            {
                SrcSubpass = Vk.SubpassExternal,
                DstSubpass = 0,
                SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
                SrcAccessMask = 0,
                DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
                DstAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.DepthStencilAttachmentWriteBit
            };

            var attachments = stackalloc AttachmentDescription[] { colorAttachment, depthAttachment };

            var renderPassInfo = new RenderPassCreateInfo
            {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = 2,
                PAttachments = attachments,
                SubpassCount = 1,
                PSubpasses = &subpass,
                DependencyCount = 1,
                PDependencies = &dependency
            };

            CheckVkResult(_vk.CreateRenderPass(_device, in renderPassInfo, null, out _vkRenderPass));
        }

        private void InitializeDirectX(Microsoft.UI.Xaml.Controls.SwapChainPanel panel, int width, int height)
        {
            // 1. Create D3D11 Device
            var featureLevels = new[]
            {
                Vortice.Direct3D.FeatureLevel.Level_11_1,
                Vortice.Direct3D.FeatureLevel.Level_11_0,
            };

            D3D11.D3D11CreateDevice(
                null,
                Vortice.Direct3D.DriverType.Hardware,
                Vortice.Direct3D11.DeviceCreationFlags.BgraSupport,
                featureLevels,
                out _d3dDevice,
                out _d3dContext).CheckError();

            // 2. Create SwapChain for WinUI
            var dxgiDevice = _d3dDevice!.QueryInterface<IDXGIDevice>();
            var dxgiAdapter = dxgiDevice.GetAdapter();
            var dxgiFactory = dxgiAdapter.GetParent<IDXGIFactory2>();

            var swapChainDesc = new SwapChainDescription1
            {
                Width = width,
                Height = height,
                Format = DxgiFormat.B8G8R8A8_UNorm,
                Stereo = false,
                SampleDescription = new SampleDescription(1, 0),
                BufferUsage = Vortice.DXGI.Usage.RenderTargetOutput,
                BufferCount = 2,
                Scaling = Scaling.Stretch,
                SwapEffect = SwapEffect.FlipSequential,
                AlphaMode = Vortice.DXGI.AlphaMode.Premultiplied,
                Flags = SwapChainFlags.None
            };

            _swapChain = dxgiFactory.CreateSwapChainForComposition(dxgiDevice, swapChainDesc);

            // 3. Associate SwapChain with SwapChainPanel
            var panelNative = GetSwapChainPanelNative(panel);
            // Use Vortice's NativePointer property which returns the correct COM pointer
            panelNative.SetSwapChain(_swapChain.NativePointer);

            // 4. Create Shared Texture (The bridge between Vulkan and DX11)
            var texDesc = new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = DxgiFormat.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = Vortice.Direct3D11.ResourceUsage.Default,
                BindFlags = Vortice.Direct3D11.BindFlags.ShaderResource | Vortice.Direct3D11.BindFlags.RenderTarget,
                CPUAccessFlags = Vortice.Direct3D11.CpuAccessFlags.None,
                MiscFlags = Vortice.Direct3D11.ResourceOptionFlags.SharedNTHandle | Vortice.Direct3D11.ResourceOptionFlags.SharedKeyedMutex
            };

            _sharedTexture = _d3dDevice.CreateTexture2D(texDesc);

            // 5. Get Shared Handle (NT Handle)
            var resource = _sharedTexture.QueryInterface<IDXGIResource1>();
            _sharedHandle = resource.CreateSharedHandle(null, DxgiSharedResourceFlags.Read | DxgiSharedResourceFlags.Write, null);
            
            resource.Dispose();
            dxgiFactory.Dispose();
            dxgiAdapter.Dispose();
            dxgiDevice.Dispose();
        }

        private static ISwapChainPanelNative GetSwapChainPanelNative(Microsoft.UI.Xaml.Controls.SwapChainPanel panel)
        {
            // Get IUnknown pointer from the WinRT object
            IntPtr pointer = Marshal.GetIUnknownForObject(panel);
            if (pointer == IntPtr.Zero)
                throw new InvalidOperationException("Failed to get IUnknown from SwapChainPanel");

            try
            {
                // Query for ISwapChainPanelNative interface
                Guid guid = new Guid("63aad0b8-7c24-40ff-85a8-640d944cc325");
                int hr = Marshal.QueryInterface(pointer, ref guid, out IntPtr nativePtr);
                if (hr != 0 || nativePtr == IntPtr.Zero)
                    throw new InvalidOperationException($"Failed to query ISwapChainPanelNative. HRESULT: 0x{hr:X8}");

                // Create RCW for the native interface
                return (ISwapChainPanelNative)Marshal.GetObjectForIUnknown(nativePtr);
            }
            finally
            {
                Marshal.Release(pointer);
            }
        }

        private void InitializeVulkan()
        {
            // 1. Create Instance
            var appNamePtr = (byte*)Marshal.StringToHGlobalAnsi("Pivot");
            var engineNamePtr = (byte*)Marshal.StringToHGlobalAnsi("No Engine");
            
            var appInfo = new ApplicationInfo
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = appNamePtr,
                ApplicationVersion = new Version32(1, 0, 0),
                PEngineName = engineNamePtr,
                EngineVersion = new Version32(1, 0, 0),
                ApiVersion = Vk.Version12
            };

            var extensions = new List<string>
            {
                KhrExternalMemoryCapabilities.ExtensionName,
                KhrGetPhysicalDeviceProperties2.ExtensionName,
                KhrWin32Surface.ExtensionName,
                "VK_KHR_surface"
            };

            var ppExtensions = (byte**)SilkMarshal.StringArrayToPtr(extensions.ToArray());

            var instanceCreateInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledExtensionCount = (uint)extensions.Count,
                PpEnabledExtensionNames = ppExtensions,
            };

            CheckVkResult(_vk.CreateInstance(in instanceCreateInfo, null, out _instance));
            
            Marshal.FreeHGlobal((IntPtr)appNamePtr);
            Marshal.FreeHGlobal((IntPtr)engineNamePtr);
            SilkMarshal.Free((nint)ppExtensions);

            // Load Instance Extensions
            if (!_vk.TryGetInstanceExtension(_instance, out _khrWin32Surface))
            {
                // Handle error
            }

            // 2. Pick Physical Device
            uint deviceCount = 0;
            _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, null);
            var devices = new PhysicalDevice[deviceCount];
            fixed (PhysicalDevice* pDevices = devices)
            {
                _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, pDevices);
            }
            _physicalDevice = devices[0]; // Just pick the first one for now

            // 3. Create Logical Device
            var deviceExtensions = new List<string>
            {
                "VK_KHR_external_memory",
                KhrExternalMemoryWin32.ExtensionName,
                "VK_KHR_win32_keyed_mutex"
            };

            var ppDeviceExtensions = (byte**)SilkMarshal.StringArrayToPtr(deviceExtensions.ToArray());

            var queuePriority = 1.0f;
            var queueCreateInfo = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = 0, // Assume graphics queue is 0 for simplicity (should query)
                QueueCount = 1,
                PQueuePriorities = &queuePriority
            };

            var deviceCreateInfo = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &queueCreateInfo,
                EnabledExtensionCount = (uint)deviceExtensions.Count,
                PpEnabledExtensionNames = ppDeviceExtensions
            };

            CheckVkResult(_vk.CreateDevice(_physicalDevice, in deviceCreateInfo, null, out _device));
            SilkMarshal.Free((nint)ppDeviceExtensions);

            _vk.GetDeviceQueue(_device, 0, 0, out _graphicsQueue);

            // Load Device Extensions
            if (!_vk.TryGetDeviceExtension(_instance, _device, out _khrExternalMemoryWin32))
            {
                throw new Exception("VK_KHR_external_memory_win32 not found");
            }
        }

        private void ImportSharedTexture(int width, int height)
        {
            // Import the NT Handle
            var memoryImportInfo = new ImportMemoryWin32HandleInfoKHR
            {
                SType = StructureType.ImportMemoryWin32HandleInfoKhr,
                HandleType = ExternalMemoryHandleTypeFlags.D3D11TextureBit,
                Handle = _sharedHandle
            };

            var imageCreateInfo = new ImageCreateInfo
            {
                SType = StructureType.ImageCreateInfo,
                ImageType = ImageType.Type2D,
                Format = VkFormat.B8G8R8A8Unorm,
                Extent = new Extent3D((uint)width, (uint)height, 1),
                MipLevels = 1,
                ArrayLayers = 1,
                Samples = SampleCountFlags.Count1Bit,
                Tiling = ImageTiling.Optimal,
                Usage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit,
                SharingMode = SharingMode.Exclusive,
                InitialLayout = ImageLayout.Undefined
            };

            // We need to specify that this image can be backed by external memory
            var externalMemoryImageCreateInfo = new ExternalMemoryImageCreateInfo
            {
                SType = StructureType.ExternalMemoryImageCreateInfo,
                HandleTypes = ExternalMemoryHandleTypeFlags.D3D11TextureBit
            };
            imageCreateInfo.PNext = &externalMemoryImageCreateInfo;

            CheckVkResult(_vk.CreateImage(_device, in imageCreateInfo, null, out _vkImage));

            // Get Memory Requirements
            _vk.GetImageMemoryRequirements(_device, _vkImage, out var memReqs);

            // Allocate Memory (Importing)
            var allocInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memReqs.Size,
                PNext = &memoryImportInfo
            };
            
            // We need to find a memory type index that supports the D3D11 memory.
            var handleProps = new MemoryWin32HandlePropertiesKHR
            {
                SType = StructureType.MemoryWin32HandlePropertiesKhr
            };
            _khrExternalMemoryWin32!.GetMemoryWin32HandleProperties(_device, ExternalMemoryHandleTypeFlags.D3D11TextureBit, _sharedHandle, out handleProps);

            // Find index
            uint memoryTypeIndex = 0;
            bool found = false;
            for (int i = 0; i < 32; i++)
            {
                if ((handleProps.MemoryTypeBits & (1 << i)) != 0)
                {
                    memoryTypeIndex = (uint)i;
                    found = true;
                    break;
                }
            }

            if (!found) throw new Exception("No compatible memory type found for shared handle");

            allocInfo.MemoryTypeIndex = memoryTypeIndex;

            CheckVkResult(_vk.AllocateMemory(_device, in allocInfo, null, out _vkDeviceMemory));
            CheckVkResult(_vk.BindImageMemory(_device, _vkImage, _vkDeviceMemory, 0));

            // Create ImageView
            var viewInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = _vkImage,
                ViewType = ImageViewType.Type2D,
                Format = VkFormat.B8G8R8A8Unorm,
                Components = new ComponentMapping(ComponentSwizzle.Identity, ComponentSwizzle.Identity, ComponentSwizzle.Identity, ComponentSwizzle.Identity),
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
            };

            CheckVkResult(_vk.CreateImageView(_device, in viewInfo, null, out _vkImageView));
        }

        private void CreateFramebuffer(int width, int height)
        {
            var attachments = stackalloc ImageView[] { _vkImageView, _vkDepthImageView };
            
            var framebufferInfo = new FramebufferCreateInfo
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _vkRenderPass,
                AttachmentCount = 2,
                PAttachments = attachments,
                Width = (uint)width,
                Height = (uint)height,
                Layers = 1
            };


            CheckVkResult(_vk.CreateFramebuffer(_device, in framebufferInfo, null, out _vkFramebuffer));
        }

        private void CreatePipeline()
        {
            // Load SPIR-V from disk
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var vertPath = Path.Combine(baseDir, "Shaders", "vert.spv");
            var fragPath = Path.Combine(baseDir, "Shaders", "frag.spv");

            var vertBytes = File.ReadAllBytes(vertPath); 
            var fragBytes = File.ReadAllBytes(fragPath);

            // Create shader modules
            fixed (byte* vertCode = vertBytes)
            fixed (byte* fragCode = fragBytes)
            {
                var vertCreateInfo = new ShaderModuleCreateInfo
                {
                    SType = StructureType.ShaderModuleCreateInfo,
                    CodeSize = (nuint)vertBytes.Length,
                    PCode = (uint*)vertCode
                };
                CheckVkResult(_vk.CreateShaderModule(_device, in vertCreateInfo, null, out _vkVertShaderModule));

                var fragCreateInfo = new ShaderModuleCreateInfo
                {
                    SType = StructureType.ShaderModuleCreateInfo,
                    CodeSize = (nuint)fragBytes.Length,
                    PCode = (uint*)fragCode
                };
                CheckVkResult(_vk.CreateShaderModule(_device, in fragCreateInfo, null, out _vkFragShaderModule));
            }

            // Shader stages
            var mainName = (byte*)Marshal.StringToHGlobalAnsi("main");
            
            var shaderStages = stackalloc PipelineShaderStageCreateInfo[2];
            shaderStages[0] = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = _vkVertShaderModule,
                PName = mainName
            };
            shaderStages[1] = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = _vkFragShaderModule,
                PName = mainName
            };

            // Vertex input: Use Vertex struct
            var bindingDescription = Vertex.GetBindingDescription();
            var attributeDescriptions = Vertex.GetAttributeDescriptions();

            fixed (VertexInputAttributeDescription* pAttributeDescriptions = attributeDescriptions)
            {
                var vertexInputInfo = new PipelineVertexInputStateCreateInfo
                {
                    SType = StructureType.PipelineVertexInputStateCreateInfo,
                    VertexBindingDescriptionCount = 1,
                    PVertexBindingDescriptions = &bindingDescription,
                    VertexAttributeDescriptionCount = (uint)attributeDescriptions.Length,
                    PVertexAttributeDescriptions = pAttributeDescriptions
                };

                var inputAssembly = new PipelineInputAssemblyStateCreateInfo
                {
                    SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                    Topology = PrimitiveTopology.TriangleList,
                    PrimitiveRestartEnable = false
                };

                var viewport = new Viewport(0, 0, _currentWidth, _currentHeight, 0, 1);
                var scissor = new Rect2D(new Offset2D(0, 0), new Extent2D((uint)_currentWidth, (uint)_currentHeight));

                var viewportState = new PipelineViewportStateCreateInfo
                {
                    SType = StructureType.PipelineViewportStateCreateInfo,
                    ViewportCount = 1,
                    PViewports = &viewport,
                    ScissorCount = 1,
                    PScissors = &scissor
                };

                var rasterizer = new PipelineRasterizationStateCreateInfo
                {
                    SType = StructureType.PipelineRasterizationStateCreateInfo,
                    DepthClampEnable = false,
                    RasterizerDiscardEnable = false,
                    PolygonMode = PolygonMode.Fill,
                    LineWidth = 1.0f,
                    CullMode = _backfaceCulling ? CullModeFlags.BackBit : CullModeFlags.None,
                    FrontFace = FrontFace.CounterClockwise,
                    DepthBiasEnable = false
                };

                var multisampling = new PipelineMultisampleStateCreateInfo
                {
                    SType = StructureType.PipelineMultisampleStateCreateInfo,
                    SampleShadingEnable = false,
                    RasterizationSamples = SampleCountFlags.Count1Bit
                };

                var depthStencil = new PipelineDepthStencilStateCreateInfo
                {
                    SType = StructureType.PipelineDepthStencilStateCreateInfo,
                    DepthTestEnable = true,
                    DepthWriteEnable = true,
                    DepthCompareOp = CompareOp.Less,
                    DepthBoundsTestEnable = false,
                    StencilTestEnable = false
                };

                var colorBlendAttachment = new PipelineColorBlendAttachmentState
                {
                    ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
                    BlendEnable = false
                };

                var colorBlending = new PipelineColorBlendStateCreateInfo
                {
                    SType = StructureType.PipelineColorBlendStateCreateInfo,
                    LogicOpEnable = false,
                    AttachmentCount = 1,
                    PAttachments = &colorBlendAttachment
                };

                var dynamicStates = stackalloc DynamicState[2] { DynamicState.Viewport, DynamicState.Scissor };
                var dynamicState = new PipelineDynamicStateCreateInfo
                {
                    SType = StructureType.PipelineDynamicStateCreateInfo,
                    DynamicStateCount = 2,
                    PDynamicStates = dynamicStates
                };

                // Pipeline Layout with Descriptor Set Layout
                fixed (DescriptorSetLayout* pSetLayouts = &_vkDescriptorSetLayout)
                {
                    var pipelineLayoutInfo = new PipelineLayoutCreateInfo
                    {
                        SType = StructureType.PipelineLayoutCreateInfo,
                        SetLayoutCount = 1,
                        PSetLayouts = pSetLayouts,
                        PushConstantRangeCount = 0
                    };
                    CheckVkResult(_vk.CreatePipelineLayout(_device, in pipelineLayoutInfo, null, out _vkPipelineLayout));
                }

                var pipelineInfo = new GraphicsPipelineCreateInfo
                {
                    SType = StructureType.GraphicsPipelineCreateInfo,
                    StageCount = 2,
                    PStages = shaderStages,
                    PVertexInputState = &vertexInputInfo,
                    PInputAssemblyState = &inputAssembly,
                    PViewportState = &viewportState,
                    PRasterizationState = &rasterizer,
                    PMultisampleState = &multisampling,
                    PDepthStencilState = &depthStencil,
                    PColorBlendState = &colorBlending,
                    PDynamicState = &dynamicState,
                    Layout = _vkPipelineLayout,
                    RenderPass = _vkRenderPass,
                    Subpass = 0
                };

                CheckVkResult(_vk.CreateGraphicsPipelines(_device, default, 1, in pipelineInfo, null, out _vkPipeline));
            }

            Marshal.FreeHGlobal((IntPtr)mainName);
        }
        // Mesh data
        private uint _indexCount = 3; // Default for initial triangle
        
        private void CreateVertexBuffer()
        {
            // Default triangle for initial display
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
            _vk.DeviceWaitIdle(_device);
            
            // Cleanup old buffers
            if (_vkVertexBuffer.Handle != 0)
            {
                _vk.DestroyBuffer(_device, _vkVertexBuffer, null);
                _vk.FreeMemory(_device, _vkVertexBufferMemory, null);
            }
            if (_vkIndexBuffer.Handle != 0)
            {
                _vk.DestroyBuffer(_device, _vkIndexBuffer, null);
                _vk.FreeMemory(_device, _vkIndexBufferMemory, null);
            }
            
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
            
            // Wait for GPU to finish all operations
            _vk.DeviceWaitIdle(_device);
            
            // Cleanup old buffers
            if (_vkVertexBuffer.Handle != 0)
            {
                _vk.DestroyBuffer(_device, _vkVertexBuffer, null);
                _vk.FreeMemory(_device, _vkVertexBufferMemory, null);
            }
            if (_vkIndexBuffer.Handle != 0)
            {
                _vk.DestroyBuffer(_device, _vkIndexBuffer, null);
                _vk.FreeMemory(_device, _vkIndexBufferMemory, null);
            }
            
            UploadMeshData(vertices, indices);
        }

        private void UploadMeshData(Vertex[] vertices, uint[] indices)
        {
            _indexCount = (uint)indices.Length;
            
            // Vertex Buffer
            ulong vertexBufferSize = (ulong)(vertices.Length * Marshal.SizeOf<Vertex>());
            CreateBuffer(vertexBufferSize, BufferUsageFlags.VertexBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out _vkVertexBuffer, out _vkVertexBufferMemory);

            void* vertexData;
            CheckVkResult(_vk.MapMemory(_device, _vkVertexBufferMemory, 0, vertexBufferSize, 0, &vertexData));
            fixed (Vertex* ptr = vertices)
            {
                System.Buffer.MemoryCopy(ptr, vertexData, vertexBufferSize, vertexBufferSize);
            }
            _vk.UnmapMemory(_device, _vkVertexBufferMemory);

            // Index Buffer
            ulong indexBufferSize = (ulong)(indices.Length * sizeof(uint));
            CreateBuffer(indexBufferSize, BufferUsageFlags.IndexBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out _vkIndexBuffer, out _vkIndexBufferMemory);

            void* indexData;
            CheckVkResult(_vk.MapMemory(_device, _vkIndexBufferMemory, 0, indexBufferSize, 0, &indexData));
            fixed (uint* ptr = indices)
            {
                System.Buffer.MemoryCopy(ptr, indexData, indexBufferSize, indexBufferSize);
            }
            _vk.UnmapMemory(_device, _vkIndexBufferMemory);
        }

        private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
        {
            _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, out var memProperties);

            for (uint i = 0; i < memProperties.MemoryTypeCount; i++)
            {
                if ((typeFilter & (1 << (int)i)) != 0 &&
                    (memProperties.MemoryTypes[(int)i].PropertyFlags & properties) == properties)
                {
                    return i;
                }
            }

            throw new Exception("Failed to find suitable memory type!");
        }

        private void CreateCommandBuffers()
        {
            var poolInfo = new CommandPoolCreateInfo
            {
                SType = StructureType.CommandPoolCreateInfo,
                QueueFamilyIndex = 0, // Graphics Queue
                Flags = CommandPoolCreateFlags.ResetCommandBufferBit
            };

            CheckVkResult(_vk.CreateCommandPool(_device, in poolInfo, null, out _vkCommandPool));

            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = _vkCommandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = 1
            };

            CheckVkResult(_vk.AllocateCommandBuffers(_device, in allocInfo, out _vkCommandBuffer));
        }

        private void CreateSyncObjects()
        {
            var fenceInfo = new FenceCreateInfo
            {
                SType = StructureType.FenceCreateInfo,
                Flags = FenceCreateFlags.SignaledBit
            };

            CheckVkResult(_vk.CreateFence(_device, in fenceInfo, null, out _vkFence));
        }

        public void Render(OrbitCamera camera)
        {
            if (_disposed) return;

            // 1. Wait for Fence
            _vk.WaitForFences(_device, 1, in _vkFence, true, ulong.MaxValue);
            _vk.ResetFences(_device, 1, in _vkFence);

            // 1. Update UBO
            UpdateUniformBuffer((float)_currentWidth / _currentHeight, camera);
            UpdateShadingBuffer();

            // 2. Record Command Buffer
            _vk.ResetCommandBuffer(_vkCommandBuffer, 0);

            var beginInfo = new CommandBufferBeginInfo
            {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.OneTimeSubmitBit
            };

            CheckVkResult(_vk.BeginCommandBuffer(_vkCommandBuffer, in beginInfo));

            var clearValues = stackalloc ClearValue[2];
            clearValues[0].Color = new ClearColorValue { Float32_0 = _bgColorR, Float32_1 = _bgColorG, Float32_2 = _bgColorB, Float32_3 = 1.0f };
            clearValues[1].DepthStencil = new ClearDepthStencilValue { Depth = 1.0f, Stencil = 0 };

            var renderPassInfo = new RenderPassBeginInfo
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = _vkRenderPass,
                Framebuffer = _vkFramebuffer,
                RenderArea =
                {
                    Offset = { X = 0, Y = 0 },
                    Extent = { Width = (uint)_currentWidth, Height = (uint)_currentHeight }
                },
                ClearValueCount = 2,
                PClearValues = clearValues
            };

            _vk.CmdBeginRenderPass(_vkCommandBuffer, in renderPassInfo, SubpassContents.Inline);
            
            // Bind the graphics pipeline
            _vk.CmdBindPipeline(_vkCommandBuffer, PipelineBindPoint.Graphics, _vkPipeline);
            
            // Set dynamic viewport and scissor
            var viewport = new Viewport(0, 0, _currentWidth, _currentHeight, 0, 1);
            _vk.CmdSetViewport(_vkCommandBuffer, 0, 1, in viewport);
            
            var scissor = new Rect2D(new Offset2D(0, 0), new Extent2D((uint)_currentWidth, (uint)_currentHeight));
            _vk.CmdSetScissor(_vkCommandBuffer, 0, 1, in scissor);
            
            // Bind descriptor sets
            fixed (DescriptorSet* pDescriptorSets = &_vkDescriptorSet)
            {
                _vk.CmdBindDescriptorSets(_vkCommandBuffer, PipelineBindPoint.Graphics, _vkPipelineLayout, 0, 1, pDescriptorSets, 0, null);
            }

            // Bind vertex buffer
            var offset = 0ul;
            var vertexBuffer = _vkVertexBuffer;
            _vk.CmdBindVertexBuffers(_vkCommandBuffer, 0, 1, in vertexBuffer, in offset);
            
            // Bind index buffer
            _vk.CmdBindIndexBuffer(_vkCommandBuffer, _vkIndexBuffer, 0, IndexType.Uint32);
            
            // Draw indexed
            _vk.CmdDrawIndexed(_vkCommandBuffer, _indexCount, 1, 0, 0, 0);
            
            _vk.CmdEndRenderPass(_vkCommandBuffer);
            CheckVkResult(_vk.EndCommandBuffer(_vkCommandBuffer));

            // 3. Submit with Keyed Mutex
            // Vulkan: Acquire Key 0, Release Key 1
            
            // Allocate memory for mutex arrays
            var acquireKeys = stackalloc ulong[1];
            var acquireTimeouts = stackalloc uint[1];
            var releaseKeys = stackalloc ulong[1];
            
            acquireKeys[0] = 0;
            acquireTimeouts[0] = uint.MaxValue; // INFINITE
            releaseKeys[0] = 1;
            
            fixed (DeviceMemory* pDeviceMemory = &_vkDeviceMemory)
            {
                var mutexInfo = new Win32KeyedMutexAcquireReleaseInfoKHR
                {
                    SType = StructureType.Win32KeyedMutexAcquireReleaseInfoKhr,
                    AcquireCount = 1,
                    PAcquireSyncs = pDeviceMemory,
                    PAcquireKeys = acquireKeys,
                    PAcquireTimeouts = acquireTimeouts,
                    ReleaseCount = 1,
                    PReleaseSyncs = pDeviceMemory,
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

                    CheckVkResult(_vk.QueueSubmit(_graphicsQueue, 1, in submitInfo, _vkFence));
                }
            }

            // 4. DX11 Present
            // DX11: Acquire Key 1, Copy, Release Key 0
            
            var keyedMutex = _sharedTexture!.QueryInterface<IDXGIKeyedMutex>();
            keyedMutex.AcquireSync(1, int.MaxValue);
            
            // Copy to BackBuffer
            var backBuffer = _swapChain!.GetBuffer<ID3D11Texture2D>(0);
            _d3dContext!.CopyResource(backBuffer, _sharedTexture);
            
            keyedMutex.ReleaseSync(0);
            
            _swapChain.Present(1, PresentFlags.None);
            
            keyedMutex.Dispose();
            backBuffer.Dispose();
        }

        public void Resize(int width, int height)
        {
            if (_disposed) return;
            
            // Wait for GPU
            _vk.DeviceWaitIdle(_device);

            // Dispose old Vulkan resources
            _vk.DestroyFramebuffer(_device, _vkFramebuffer, null);
            _vk.DestroyImageView(_device, _vkImageView, null);
            _vk.FreeMemory(_device, _vkDeviceMemory, null);
            _vk.DestroyImage(_device, _vkImage, null);
            
            // Dispose old depth resources
            _vk.DestroyImageView(_device, _vkDepthImageView, null);
            _vk.FreeMemory(_device, _vkDepthImageMemory, null);
            _vk.DestroyImage(_device, _vkDepthImage, null);
            
            _sharedTexture?.Dispose();
            _swapChain?.ResizeBuffers(2, width, height, DxgiFormat.B8G8R8A8_UNorm, SwapChainFlags.None);
            
            // Re-create Shared Texture
            var texDesc = new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = DxgiFormat.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = Vortice.Direct3D11.ResourceUsage.Default,
                BindFlags = Vortice.Direct3D11.BindFlags.ShaderResource | Vortice.Direct3D11.BindFlags.RenderTarget,
                CPUAccessFlags = Vortice.Direct3D11.CpuAccessFlags.None,
                MiscFlags = Vortice.Direct3D11.ResourceOptionFlags.SharedNTHandle | Vortice.Direct3D11.ResourceOptionFlags.SharedKeyedMutex
            };
            _sharedTexture = _d3dDevice!.CreateTexture2D(texDesc);
            
            // Re-get Handle
            var resource = _sharedTexture.QueryInterface<IDXGIResource1>();
            _sharedHandle = resource.CreateSharedHandle(null, DxgiSharedResourceFlags.Read | DxgiSharedResourceFlags.Write, null);
            resource.Dispose();

            // Update dimensions before recreating resources
            _currentWidth = width;
            _currentHeight = height;

            // Re-import to Vulkan
            ImportSharedTexture(width, height);
            
            // Recreate depth buffer at new size
            CreateDepthResources();
            
            CreateFramebuffer(width, height);
        }

        private void CreateDepthResources()
        {
            var depthFormat = FindDepthFormat();
            
            CreateImage((uint)_currentWidth, (uint)_currentHeight, depthFormat, ImageTiling.Optimal, 
                ImageUsageFlags.DepthStencilAttachmentBit, MemoryPropertyFlags.DeviceLocalBit, 
                out _vkDepthImage, out _vkDepthImageMemory);

            _vkDepthImageView = CreateImageView(_vkDepthImage, depthFormat, ImageAspectFlags.DepthBit);
        }

        private VkFormat FindDepthFormat()
        {
            return FindSupportedFormat(
                new[] { VkFormat.D32Sfloat, VkFormat.D32SfloatS8Uint, VkFormat.D24UnormS8Uint },
                ImageTiling.Optimal,
                FormatFeatureFlags.DepthStencilAttachmentBit
            );
        }

        private VkFormat FindSupportedFormat(IEnumerable<VkFormat> candidates, ImageTiling tiling, FormatFeatureFlags features)
        {
            foreach (var format in candidates)
            {
                _vk.GetPhysicalDeviceFormatProperties(_physicalDevice, format, out var props);

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

        private void CreateImage(uint width, uint height, VkFormat format, ImageTiling tiling, ImageUsageFlags usage, MemoryPropertyFlags properties, out Image image, out DeviceMemory imageMemory)
        {
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
                Flags = 0 // Optional
            };

            CheckVkResult(_vk.CreateImage(_device, in imageInfo, null, out image));

            _vk.GetImageMemoryRequirements(_device, image, out var memRequirements);

            var allocInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memRequirements.Size,
                MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, properties)
            };

            CheckVkResult(_vk.AllocateMemory(_device, in allocInfo, null, out imageMemory));
            CheckVkResult(_vk.BindImageMemory(_device, image, imageMemory, 0));
        }
        
        private ImageView CreateImageView(Image image, VkFormat format, ImageAspectFlags aspectFlags)
        {
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
            CheckVkResult(_vk.CreateImageView(_device, in createInfo, null, out var imageView));
            return imageView;
        }

        // Shading Mode
        private ShadingMode _currentShadingMode = ShadingMode.WorldNormal;
        private Silk.NET.Vulkan.Buffer _vkShadingBuffer;
        private DeviceMemory _vkShadingBufferMemory;
        private void* _vkShadingBufferMapped;

        // Background color (RGB)
        private float _bgColorR = 0.2f;
        private float _bgColorG = 0.6f;
        private float _bgColorB = 0.8f;

        // Light parameters
        private System.Numerics.Vector3 _lightDirection = System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(0.5f, 1.0f, 0.3f));
        private float _lightIntensity = 1.0f;
        private System.Numerics.Vector3 _lightColor = new System.Numerics.Vector3(1f, 1f, 1f);

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
        }

        public void SetShadingMode(ShadingMode mode)
        {
            _currentShadingMode = mode;
        }

        public void SetBackgroundColor(float r, float g, float b)
        {
            _bgColorR = r;
            _bgColorG = g;
            _bgColorB = b;
        }

        public void SetLightParams(System.Numerics.Vector3 direction, float intensity, System.Numerics.Vector3 color)
        {
            _lightDirection = System.Numerics.Vector3.Normalize(direction);
            _lightIntensity = intensity;
            _lightColor = color;
        }

        private void CreateDescriptorSetLayout()
        {
            var uboLayoutBinding = new DescriptorSetLayoutBinding
            {
                Binding = 0,
                DescriptorType = DescriptorType.UniformBuffer,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.VertexBit,
                PImmutableSamplers = null
            };

            var shadingLayoutBinding = new DescriptorSetLayoutBinding
            {
                Binding = 1,
                DescriptorType = DescriptorType.UniformBuffer,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.FragmentBit,
                PImmutableSamplers = null
            };

            var bindings = stackalloc DescriptorSetLayoutBinding[] { uboLayoutBinding, shadingLayoutBinding };

            var layoutInfo = new DescriptorSetLayoutCreateInfo
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = 2,
                PBindings = bindings
            };

            CheckVkResult(_vk.CreateDescriptorSetLayout(_device, in layoutInfo, null, out _vkDescriptorSetLayout));
        }

        private void CreateUniformBuffers()
        {
            // 1. Camera UBO
            ulong bufferSize = (ulong)Marshal.SizeOf<UniformBufferObject>();
            CreateBuffer(bufferSize, BufferUsageFlags.UniformBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out _vkUniformBuffer, out _vkUniformBufferMemory);
            
            void* pMapped;
            CheckVkResult(_vk.MapMemory(_device, _vkUniformBufferMemory, 0, bufferSize, 0, &pMapped));
            _vkUniformBufferMapped = pMapped;

            // 2. Shading Params UBO
            ulong shadingBufferSize = (ulong)Marshal.SizeOf<ShadingParams>();
            CreateBuffer(shadingBufferSize, BufferUsageFlags.UniformBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out _vkShadingBuffer, out _vkShadingBufferMemory);
            
            void* pShadingMapped;
            CheckVkResult(_vk.MapMemory(_device, _vkShadingBufferMemory, 0, shadingBufferSize, 0, &pShadingMapped));
            _vkShadingBufferMapped = pShadingMapped;
        }

        private void CreateDescriptorPool()
        {
            var poolSizes = stackalloc DescriptorPoolSize[] 
            { 
                new DescriptorPoolSize { Type = DescriptorType.UniformBuffer, DescriptorCount = 2 } 
            };

            var poolInfo = new DescriptorPoolCreateInfo
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                PoolSizeCount = 1,
                PPoolSizes = poolSizes,
                MaxSets = 1
            };

            CheckVkResult(_vk.CreateDescriptorPool(_device, in poolInfo, null, out _vkDescriptorPool));
        }

        private void CreateDescriptorSets()
        {
            var layouts = stackalloc DescriptorSetLayout[] { _vkDescriptorSetLayout };
            var allocInfo = new DescriptorSetAllocateInfo
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = _vkDescriptorPool,
                DescriptorSetCount = 1,
                PSetLayouts = layouts
            };

            CheckVkResult(_vk.AllocateDescriptorSets(_device, in allocInfo, out _vkDescriptorSet));

            // 1. Camera UBO
            var bufferInfo = new DescriptorBufferInfo
            {
                Buffer = _vkUniformBuffer,
                Offset = 0,
                Range = (ulong)Marshal.SizeOf<UniformBufferObject>()
            };

            // 2. Shading Params UBO
            var shadingBufferInfo = new DescriptorBufferInfo
            {
                Buffer = _vkShadingBuffer,
                Offset = 0,
                Range = (ulong)Marshal.SizeOf<ShadingParams>()
            };

            var descriptorWrites = stackalloc WriteDescriptorSet[2];

            descriptorWrites[0] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _vkDescriptorSet,
                DstBinding = 0,
                DstArrayElement = 0,
                DescriptorType = DescriptorType.UniformBuffer,
                DescriptorCount = 1,
                PBufferInfo = &bufferInfo
            };

            descriptorWrites[1] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _vkDescriptorSet,
                DstBinding = 1,
                DstArrayElement = 0,
                DescriptorType = DescriptorType.UniformBuffer,
                DescriptorCount = 1,
                PBufferInfo = &shadingBufferInfo
            };

            _vk.UpdateDescriptorSets(_device, 2, descriptorWrites, 0, null);
        }

        public void UpdateUniformBuffer(float aspectRatio, OrbitCamera camera)
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

        private void CreateBuffer(ulong size, BufferUsageFlags usage, MemoryPropertyFlags properties, out Silk.NET.Vulkan.Buffer buffer, out DeviceMemory bufferMemory)
        {
            var bufferInfo = new BufferCreateInfo
            {
                SType = StructureType.BufferCreateInfo,
                Size = size,
                Usage = usage,
                SharingMode = SharingMode.Exclusive
            };

            CheckVkResult(_vk.CreateBuffer(_device, in bufferInfo, null, out buffer));

            _vk.GetBufferMemoryRequirements(_device, buffer, out var memRequirements);

            var allocInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memRequirements.Size,
                MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, properties)
            };

            CheckVkResult(_vk.AllocateMemory(_device, in allocInfo, null, out bufferMemory));
            CheckVkResult(_vk.BindBufferMemory(_device, buffer, bufferMemory, 0));
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _vk.DeviceWaitIdle(_device);
            
            // Pipeline resources
            _vk.DestroyPipeline(_device, _vkPipeline, null);
            _vk.DestroyPipelineLayout(_device, _vkPipelineLayout, null);
            _vk.DestroyShaderModule(_device, _vkVertShaderModule, null);
            _vk.DestroyShaderModule(_device, _vkFragShaderModule, null);
            
            // Vertex buffer
            _vk.DestroyBuffer(_device, _vkVertexBuffer, null);
            _vk.FreeMemory(_device, _vkVertexBufferMemory, null);

            // Index buffer
            _vk.DestroyBuffer(_device, _vkIndexBuffer, null);
            _vk.FreeMemory(_device, _vkIndexBufferMemory, null);

            // Uniform buffer
            _vk.DestroyBuffer(_device, _vkUniformBuffer, null);
            _vk.FreeMemory(_device, _vkUniformBufferMemory, null);

            // Depth Resources
            _vk.DestroyImageView(_device, _vkDepthImageView, null);
            _vk.FreeMemory(_device, _vkDepthImageMemory, null);
            _vk.DestroyImage(_device, _vkDepthImage, null);

            // Descriptors
            _vk.DestroyDescriptorPool(_device, _vkDescriptorPool, null);
            _vk.DestroyDescriptorSetLayout(_device, _vkDescriptorSetLayout, null);
            
            _vk.DestroyFence(_device, _vkFence, null);
            _vk.DestroyCommandPool(_device, _vkCommandPool, null);
            _vk.DestroyFramebuffer(_device, _vkFramebuffer, null);
            _vk.DestroyRenderPass(_device, _vkRenderPass, null);
            _vk.DestroyImageView(_device, _vkImageView, null);
            _vk.FreeMemory(_device, _vkDeviceMemory, null);
            _vk.DestroyImage(_device, _vkImage, null);
            
            _vk.DestroyDevice(_device, null);
            _vk.DestroyInstance(_instance, null);
            
            _sharedTexture?.Dispose();
            _swapChain?.Dispose();
            _d3dDevice?.Dispose();
            
            _disposed = true;
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
                _padding2 = 0.0f
            };

            System.Buffer.MemoryCopy(&shadingParams, _vkShadingBufferMapped, (ulong)Marshal.SizeOf<ShadingParams>(), (ulong)Marshal.SizeOf<ShadingParams>());
        }
        
        /// <summary>
        /// Helper method to check Vulkan Result and throw if not successful
        /// </summary>
        private static void CheckVkResult(Result result)
        {
            if (result != Result.Success)
            {
                throw new Exception($"Vulkan error: {result}");
            }
        }
    }
}
