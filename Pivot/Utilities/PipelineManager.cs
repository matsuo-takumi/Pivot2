using System;
using System.IO;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using VkFormat = Silk.NET.Vulkan.Format;

namespace Pivot.Utilities
{
    /// <summary>
    /// Manages Vulkan graphics pipeline, shaders, render pass, framebuffer, and descriptors.
    /// </summary>
    public unsafe class PipelineManager : IDisposable
    {
        private bool _disposed;
        private readonly VulkanCore _core;

        // Pipeline Resources
        private PipelineLayout _vkPipelineLayout;
        private Pipeline _vkPipeline;
        private ShaderModule _vkVertShaderModule;
        private ShaderModule _vkFragShaderModule;
        private RenderPass _vkRenderPass;
        private Framebuffer _vkFramebuffer;

        // Descriptors
        private DescriptorSetLayout _vkDescriptorSetLayout;
        private DescriptorPool _vkDescriptorPool;
        private DescriptorSet _vkDescriptorSet;

        // Current dimensions
        private int _currentWidth;
        private int _currentHeight;

        // Rendering settings
        private bool _backfaceCulling = true;
        private bool _isWireframe = false;

        // Public accessors
        public RenderPass RenderPass => _vkRenderPass;
        public Framebuffer Framebuffer => _vkFramebuffer;
        public Pipeline Pipeline => _vkPipeline;
        public PipelineLayout PipelineLayout => _vkPipelineLayout;
        public DescriptorSetLayout DescriptorSetLayout => _vkDescriptorSetLayout;
        public DescriptorPool DescriptorPool => _vkDescriptorPool;
        public DescriptorSet DescriptorSet => _vkDescriptorSet;

        public PipelineManager(VulkanCore core)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));
        }

        /// <summary>
        /// Initialize pipeline components
        /// </summary>
        public void Initialize(int width, int height, ImageView colorImageView, ImageView depthImageView)
        {
            _currentWidth = width;
            _currentHeight = height;

            CreateDescriptorSetLayout();
            CreateRenderPass();
            CreatePipeline();
            CreateFramebuffer(colorImageView, depthImageView);
        }

        /// <summary>
        /// Set backface culling mode (requires pipeline recreation)
        /// </summary>
        public void SetBackfaceCulling(bool enabled)
        {
            if (_backfaceCulling == enabled) return;
            _backfaceCulling = enabled;

            // Recreate pipeline with new culling mode
            if (_vkPipeline.Handle != 0)
            {
                var vk = _core.Vk;
                var device = _core.Device;
                vk.DeviceWaitIdle(device);
                vk.DestroyPipeline(device, _vkPipeline, null);
                vk.DestroyPipelineLayout(device, _vkPipelineLayout, null);
                CreatePipeline();
            }
        }

        /// <summary>
        /// Set wireframe mode (requires pipeline recreation)
        /// </summary>
        public void SetWireframe(bool enabled)
        {
            if (_isWireframe == enabled) return;
            _isWireframe = enabled;

            // Recreate pipeline with new polygon mode
            if (_vkPipeline.Handle != 0)
            {
                var vk = _core.Vk;
                var device = _core.Device;
                vk.DeviceWaitIdle(device);
                vk.DestroyPipeline(device, _vkPipeline, null);
                vk.DestroyPipelineLayout(device, _vkPipelineLayout, null);
                CreatePipeline();
            }
        }

        /// <summary>
        /// Update dimensions and recreate framebuffer
        /// </summary>
        public void UpdateSize(int width, int height, ImageView colorImageView, ImageView depthImageView)
        {
            _currentWidth = width;
            _currentHeight = height;
            
            var vk = _core.Vk;
            var device = _core.Device;
            
            vk.DestroyFramebuffer(device, _vkFramebuffer, null);
            CreateFramebuffer(colorImageView, depthImageView);
        }

        public void CreateRenderPass()
        {
            var vk = _core.Vk;
            var device = _core.Device;

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

            VulkanCore.CheckVkResult(vk.CreateRenderPass(device, in renderPassInfo, null, out _vkRenderPass));
        }

        public void CreatePipeline()
        {
            var vk = _core.Vk;
            var device = _core.Device;

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
                VulkanCore.CheckVkResult(vk.CreateShaderModule(device, in vertCreateInfo, null, out _vkVertShaderModule));

                var fragCreateInfo = new ShaderModuleCreateInfo
                {
                    SType = StructureType.ShaderModuleCreateInfo,
                    CodeSize = (nuint)fragBytes.Length,
                    PCode = (uint*)fragCode
                };
                VulkanCore.CheckVkResult(vk.CreateShaderModule(device, in fragCreateInfo, null, out _vkFragShaderModule));
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
                    PolygonMode = _isWireframe ? PolygonMode.Line : PolygonMode.Fill,
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
                    VulkanCore.CheckVkResult(vk.CreatePipelineLayout(device, in pipelineLayoutInfo, null, out _vkPipelineLayout));
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

                VulkanCore.CheckVkResult(vk.CreateGraphicsPipelines(device, default, 1, in pipelineInfo, null, out _vkPipeline));
            }

            Marshal.FreeHGlobal((IntPtr)mainName);
        }

        public void CreateFramebuffer(ImageView colorImageView, ImageView depthImageView)
        {
            var vk = _core.Vk;
            var device = _core.Device;

            var attachments = stackalloc ImageView[] { colorImageView, depthImageView };

            var framebufferInfo = new FramebufferCreateInfo
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _vkRenderPass,
                AttachmentCount = 2,
                PAttachments = attachments,
                Width = (uint)_currentWidth,
                Height = (uint)_currentHeight,
                Layers = 1
            };

            VulkanCore.CheckVkResult(vk.CreateFramebuffer(device, in framebufferInfo, null, out _vkFramebuffer));
        }

        public void CreateDescriptorSetLayout()
        {
            var vk = _core.Vk;
            var device = _core.Device;

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

            VulkanCore.CheckVkResult(vk.CreateDescriptorSetLayout(device, in layoutInfo, null, out _vkDescriptorSetLayout));
        }

        public void CreateDescriptorPool()
        {
            var vk = _core.Vk;
            var device = _core.Device;

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

            VulkanCore.CheckVkResult(vk.CreateDescriptorPool(device, in poolInfo, null, out _vkDescriptorPool));
        }

        public void CreateDescriptorSets(Silk.NET.Vulkan.Buffer uniformBuffer, ulong uniformBufferSize, 
                                          Silk.NET.Vulkan.Buffer shadingBuffer, ulong shadingBufferSize)
        {
            var vk = _core.Vk;
            var device = _core.Device;

            var layouts = stackalloc DescriptorSetLayout[] { _vkDescriptorSetLayout };
            var allocInfo = new DescriptorSetAllocateInfo
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = _vkDescriptorPool,
                DescriptorSetCount = 1,
                PSetLayouts = layouts
            };

            VulkanCore.CheckVkResult(vk.AllocateDescriptorSets(device, in allocInfo, out _vkDescriptorSet));

            // 1. Camera UBO
            var bufferInfo = new DescriptorBufferInfo
            {
                Buffer = uniformBuffer,
                Offset = 0,
                Range = uniformBufferSize
            };

            // 2. Shading Params UBO
            var shadingBufferInfo = new DescriptorBufferInfo
            {
                Buffer = shadingBuffer,
                Offset = 0,
                Range = shadingBufferSize
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

            vk.UpdateDescriptorSets(device, 2, descriptorWrites, 0, null);
        }

        private VkFormat FindDepthFormat()
        {
            return FindSupportedFormat(
                new[] { VkFormat.D32Sfloat, VkFormat.D32SfloatS8Uint, VkFormat.D24UnormS8Uint },
                ImageTiling.Optimal,
                FormatFeatureFlags.DepthStencilAttachmentBit
            );
        }

        private VkFormat FindSupportedFormat(VkFormat[] candidates, ImageTiling tiling, FormatFeatureFlags features)
        {
            var vk = _core.Vk;
            var physicalDevice = _core.PhysicalDevice;

            foreach (var format in candidates)
            {
                vk.GetPhysicalDeviceFormatProperties(physicalDevice, format, out var props);

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

        public void Dispose()
        {
            if (_disposed) return;

            var vk = _core.Vk;
            var device = _core.Device;

            vk.DestroyPipeline(device, _vkPipeline, null);
            vk.DestroyPipelineLayout(device, _vkPipelineLayout, null);
            vk.DestroyShaderModule(device, _vkVertShaderModule, null);
            vk.DestroyShaderModule(device, _vkFragShaderModule, null);
            vk.DestroyFramebuffer(device, _vkFramebuffer, null);
            vk.DestroyRenderPass(device, _vkRenderPass, null);
            vk.DestroyDescriptorPool(device, _vkDescriptorPool, null);
            vk.DestroyDescriptorSetLayout(device, _vkDescriptorSetLayout, null);

            _disposed = true;
        }
    }
}
