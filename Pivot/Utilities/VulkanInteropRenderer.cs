using System;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace Pivot.Utilities
{
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
        private SurfaceKHR _surface; // Not used in Interop mode usually, but good to have if we switch
        
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
        private Semaphore _vkSemaphore;

        public VulkanInteropRenderer()
        {
            _vk = Vk.GetApi();
        }

        public void Initialize(Microsoft.UI.Xaml.Controls.SwapChainPanel panel, int width, int height)
        {
            _currentWidth = width;
            _currentHeight = height;

            InitializeDirectX(panel, width, height);
            InitializeVulkan();
            ImportSharedTexture(width, height);
            CreateRenderPass();
            CreateFramebuffer(width, height);
            CreateCommandBuffers();
            CreateSyncObjects();
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
                D3D11.DeviceCreationFlags.BgraSupport, // Important for Direct2D/WinUI interop
                featureLevels,
                out _d3dDevice,
                out _d3dContext).CheckError();

            // 2. Create SwapChain for WinUI
            // WinUI requires DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL or FLIP_DISCARD
            // and DXGI_SCALING_STRETCH
            var dxgiDevice = _d3dDevice!.QueryInterface<IDXGIDevice>();
            var dxgiAdapter = dxgiDevice.GetAdapter();
            var dxgiFactory = dxgiAdapter.GetParent<IDXGIFactory2>();

            var swapChainDesc = new SwapChainDescription1
            {
                Width = width,
                Height = height,
                Format = Format.B8G8R8A8_UNorm, // WinUI usually expects B8G8R8A8
                Stereo = false,
                SampleDescription = new SampleDescription(1, 0),
                Usage = Usage.RenderTargetOutput | Usage.BackBuffer,
                BufferCount = 2,
                Scaling = Scaling.Stretch,
                SwapEffect = SwapEffect.FlipSequential,
                AlphaMode = AlphaMode.Premultiplied,
                Flags = SwapChainFlags.None
            };

            _swapChain = dxgiFactory.CreateSwapChainForComposition(dxgiDevice, swapChainDesc);

            // 3. Associate SwapChain with SwapChainPanel
            // We need to cast the SwapChainPanel to IInspectable (object in C# is effectively IInspectable/IUnknown for COM)
            // and then query for ISwapChainPanelNative.
            // In .NET 5+, we can use ComWrappers or simple casting if the interface is defined with ComImport.
            var panelNative = panel.As<ISwapChainPanelNative>();
            panelNative.SetSwapChain(_swapChain);

            // 4. Create Shared Texture (The bridge between Vulkan and DX11)
            // This texture will be written by Vulkan and read by DX11 (copied to SwapChain backbuffer)
            var texDesc = new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm, // Must match SwapChain or be compatible
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                CpuAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.SharedNthandle | ResourceOptionFlags.SharedKeyedmutex
            };

            _sharedTexture = _d3dDevice.CreateTexture2D(texDesc);

            // 5. Get Shared Handle (NT Handle)
            var resource = _sharedTexture.QueryInterface<IDXGIResource1>();
            _sharedHandle = resource.CreateSharedHandle(null, DXGI.DXGI_SHARED_RESOURCE_READ | DXGI.DXGI_SHARED_RESOURCE_WRITE, null);
            
            resource.Dispose();
            dxgiFactory.Dispose();
            dxgiAdapter.Dispose();
            dxgiDevice.Dispose();
        }

        private void InitializeVulkan()
        {
            // 1. Create Instance
            var appInfo = new ApplicationInfo
            {
                PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("Pivot"),
                ApplicationVersion = new Version32(1, 0, 0),
                PEngineName = (byte*)Marshal.StringToHGlobalAnsi("No Engine"),
                EngineVersion = new Version32(1, 0, 0),
                ApiVersion = Vk.Version12
            };

            var extensions = new List<string>
            {
                KhrExternalMemoryCapabilities.ExtensionName,
                KhrGetPhysicalDeviceProperties2.ExtensionName,
                KhrWin32Surface.ExtensionName, // Optional but good for compatibility
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

            _vk.CreateInstance(in instanceCreateInfo, null, out _instance).CheckError();
            SilkMarshal.Free((nint)appInfo.PApplicationName);
            SilkMarshal.Free((nint)appInfo.PEngineName);
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
                KhrExternalMemory.ExtensionName,
                KhrExternalMemoryWin32.ExtensionName,
                // KhrWin32KeyedMutex.ExtensionName // If we use KeyedMutex
            };
            // Note: KeyedMutex extension might be needed if we strictly use KeyedMutex, 
            // but ExternalMemoryWin32 often implies it for D3D11 interop. 
            // Let's add it if Silk.NET has it, otherwise assume it's covered.
            // Silk.NET usually has "VK_KHR_win32_keyed_mutex" as KhrWin32KeyedMutex.
            deviceExtensions.Add("VK_KHR_win32_keyed_mutex");

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

            _vk.CreateDevice(_physicalDevice, in deviceCreateInfo, null, out _device).CheckError();
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
                HandleType = ExternalMemoryHandleTypeFlagsKHR.D3D11TextureBitKhr,
                Handle = _sharedHandle
            };

            // We need to create an Image that binds to this memory.
            // But first we need to know the memory requirements and find a suitable memory type.
            // However, for external memory, we often allocate the memory *from* the handle.
            
            // Actually, for D3D11 interop, the memory is already allocated by D3D11.
            // We need to create a VkImage and then Bind it to the imported memory.
            
            var imageCreateInfo = new ImageCreateInfo
            {
                SType = StructureType.ImageCreateInfo,
                ImageType = ImageType.Type2D,
                Format = Format.B8G8R8A8Unorm, // Match DX11
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
                HandleTypes = ExternalMemoryHandleTypeFlagsKHR.D3D11TextureBitKhr
            };
            imageCreateInfo.PNext = &externalMemoryImageCreateInfo;

            _vk.CreateImage(_device, in imageCreateInfo, null, out _vkImage).CheckError();

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
            // Usually we check `GetMemoryWin32HandlePropertiesKHR` to find the compatible type.
            var handleProps = new MemoryWin32HandlePropertiesKHR
            {
                SType = StructureType.MemoryWin32HandlePropertiesKhr
            };
            _khrExternalMemoryWin32!.GetMemoryWin32HandleProperties(_device, ExternalMemoryHandleTypeFlagsKHR.D3D11TextureBitKhr, _sharedHandle, out handleProps);

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

            _vk.AllocateMemory(_device, in allocInfo, null, out _vkDeviceMemory).CheckError();
            _vk.BindImageMemory(_device, _vkImage, _vkDeviceMemory, 0).CheckError();

            // Create ImageView
            var viewInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = _vkImage,
                ViewType = ImageViewType.Type2D,
                Format = Format.B8G8R8A8Unorm,
                Components = new ComponentMapping(ComponentSwizzle.Identity, ComponentSwizzle.Identity, ComponentSwizzle.Identity, ComponentSwizzle.Identity),
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
            };

            _vk.CreateImageView(_device, in viewInfo, null, out _vkImageView).CheckError();
        }

        private void CreateRenderPass()
        {
            var colorAttachment = new AttachmentDescription
            {
                Format = Format.B8G8R8A8Unorm,
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.ColorAttachmentOptimal // We will transition to this
            };

            var colorAttachmentRef = new AttachmentReference
            {
                Attachment = 0,
                Layout = ImageLayout.ColorAttachmentOptimal
            };

            var subpass = new SubpassDescription
            {
                PipelineBindPoint = PipelineBindPoint.Graphics,
                ColorAttachmentCount = 1,
                PColorAttachments = &colorAttachmentRef
            };

            var renderPassInfo = new RenderPassCreateInfo
            {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = 1,
                PAttachments = &colorAttachment,
                SubpassCount = 1,
                PSubpasses = &subpass
            };

            _vk.CreateRenderPass(_device, in renderPassInfo, null, out _vkRenderPass).CheckError();
        }

        private void CreateFramebuffer(int width, int height)
        {
            fixed (ImageView* pImageViews = &_vkImageView)
            {
                var framebufferInfo = new FramebufferCreateInfo
                {
                    SType = StructureType.FramebufferCreateInfo,
                    RenderPass = _vkRenderPass,
                    AttachmentCount = 1,
                    PAttachments = pImageViews,
                    Width = (uint)width,
                    Height = (uint)height,
                    Layers = 1
                };

                _vk.CreateFramebuffer(_device, in framebufferInfo, null, out _vkFramebuffer).CheckError();
            }
        }

        private void CreateCommandBuffers()
        {
            var poolInfo = new CommandPoolCreateInfo
            {
                SType = StructureType.CommandPoolCreateInfo,
                QueueFamilyIndex = 0, // Graphics Queue
                Flags = CommandPoolCreateFlags.ResetCommandBufferBit
            };

            _vk.CreateCommandPool(_device, in poolInfo, null, out _vkCommandPool).CheckError();

            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = _vkCommandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = 1
            };

            _vk.AllocateCommandBuffers(_device, in allocInfo, out _vkCommandBuffer).CheckError();
        }

        private void CreateSyncObjects()
        {
            var fenceInfo = new FenceCreateInfo
            {
                SType = StructureType.FenceCreateInfo,
                Flags = FenceCreateFlags.SignaledBit
            };

            _vk.CreateFence(_device, in fenceInfo, null, out _vkFence).CheckError();
        }

        public void Render()
        {
            if (_disposed) return;

            // 1. Wait for Fence
            _vk.WaitForFences(_device, 1, in _vkFence, true, ulong.MaxValue);
            _vk.ResetFences(_device, 1, in _vkFence);

            // 2. Record Command Buffer
            var beginInfo = new CommandBufferBeginInfo
            {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.OneTimeSubmitBit
            };

            _vk.BeginCommandBuffer(_vkCommandBuffer, in beginInfo).CheckError();

            var clearColor = new ClearValue
            {
                Color = new ClearColorValue(0.0f, 0.0f, 0.0f, 1.0f) // Black Clear
            };

            var renderPassInfo = new RenderPassBeginInfo
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = _vkRenderPass,
                Framebuffer = _vkFramebuffer,
                RenderArea = new Rect2D { Offset = new Offset2D(0, 0), Extent = new Extent2D(800, 600) }, // TODO: Update Extent dynamically
                ClearValueCount = 1,
                PClearValues = &clearColor
            };
            
            // Need to update RenderArea extent to match current size
            // We can store width/height in class or query image
            // For now let's assume we update it in Resize or pass it here.
            // Let's query the framebuffer size from the image extent we created? 
            // Or simpler: just use a stored size.
            // For now, hardcoded 800x600 is bad. Let's fix in Resize.

            _vk.CmdBeginRenderPass(_vkCommandBuffer, in renderPassInfo, SubpassContents.Inline);
            _vk.CmdEndRenderPass(_vkCommandBuffer);
            _vk.EndCommandBuffer(_vkCommandBuffer).CheckError();

            // 3. Submit with Keyed Mutex
            // Vulkan: Acquire Key 0, Release Key 1
            
            // We need the device memory handle for Keyed Mutex?
            // Actually, we need to pass the memory object(s) involved.
            
            var mutexInfo = new Win32KeyedMutexAcquireReleaseInfoKHR
            {
                SType = StructureType.Win32KeyedMutexAcquireReleaseInfoKhr,
                AcquireCount = 1,
                PAcquireSyncs = &_vkDeviceMemory,
                PAcquireKeys = (ulong*)SilkMarshal.Allocate((nint)(sizeof(ulong) * 1)), // 0
                PAcquireTimeoutMilliseconds = (uint*)SilkMarshal.Allocate((nint)(sizeof(uint) * 1)), // Infinite?
                ReleaseCount = 1,
                PReleaseSyncs = &_vkDeviceMemory,
                PReleaseKeys = (ulong*)SilkMarshal.Allocate((nint)(sizeof(ulong) * 1)) // 1
            };
            
            *(ulong*)mutexInfo.PAcquireKeys = 0;
            *(uint*)mutexInfo.PAcquireTimeoutMilliseconds = 0xFFFFFFFF; // INFINITE
            *(ulong*)mutexInfo.PReleaseKeys = 1;

            var submitInfo = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                CommandBufferCount = 1,
                PCommandBuffers = &_vkCommandBuffer,
                PNext = &mutexInfo
            };

            _vk.QueueSubmit(_graphicsQueue, 1, in submitInfo, _vkFence).CheckError();
            
            SilkMarshal.Free((nint)mutexInfo.PAcquireKeys);
            SilkMarshal.Free((nint)mutexInfo.PAcquireTimeoutMilliseconds);
            SilkMarshal.Free((nint)mutexInfo.PReleaseKeys);

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

            // Dispose old resources
            _vk.DestroyFramebuffer(_device, _vkFramebuffer, null);
            _vk.DestroyImageView(_device, _vkImageView, null);
            _vk.FreeMemory(_device, _vkDeviceMemory, null);
            _vk.DestroyImage(_device, _vkImage, null);
            
            _sharedTexture?.Dispose();
            _swapChain?.ResizeBuffers(2, width, height, Format.B8G8R8A8_UNorm, SwapChainFlags.None);
            
            // Re-create Shared Texture
             var texDesc = new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                CpuAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.SharedNthandle | ResourceOptionFlags.SharedKeyedmutex
            };
            _sharedTexture = _d3dDevice!.CreateTexture2D(texDesc);
            
            // Re-get Handle
            var resource = _sharedTexture.QueryInterface<IDXGIResource1>();
            _sharedHandle = resource.CreateSharedHandle(null, DXGI.DXGI_SHARED_RESOURCE_READ | DXGI.DXGI_SHARED_RESOURCE_WRITE, null);
            resource.Dispose();

            // Re-import to Vulkan
            ImportSharedTexture(width, height);
            CreateFramebuffer(width, height);
            
            // Update RenderArea in Render() - we need to store width/height
            _currentWidth = width;
            _currentHeight = height;
        }
        
        private int _currentWidth;
        private int _currentHeight;

        public void Dispose()
        {
            if (_disposed) return;
            
            _vk.DeviceWaitIdle(_device);
            
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
            _d3dContext?.Dispose();
            _d3dDevice?.Dispose();
            
            _disposed = true;
        }
    }
}
