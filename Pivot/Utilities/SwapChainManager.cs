using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Controls;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Vortice.Direct3D11;
using Vortice.DXGI;
using DxgiFormat = Vortice.DXGI.Format;
using VkFormat = Silk.NET.Vulkan.Format;
using VkImage = Silk.NET.Vulkan.Image;
using DxgiSharedResourceFlags = Vortice.DXGI.SharedResourceFlags;

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
        void SetSwapChain(IntPtr swapChain);
    }

    /// <summary>
    /// Manages DirectX 11 and Vulkan interop through shared textures and SwapChain.
    /// Handles DXGI SwapChain creation and shared texture management.
    /// </summary>
    public unsafe class SwapChainManager : IDisposable
    {
        private bool _disposed;
        private readonly VulkanCore _core;

        // DirectX 11
        private ID3D11Device? _d3dDevice;
        private ID3D11DeviceContext? _d3dContext;
        private IDXGISwapChain1? _swapChain;
        private ID3D11Texture2D? _sharedTexture;
        private IntPtr _sharedHandle = IntPtr.Zero;

        // Vulkan shared image resources
        private VkImage _vkImage;
        private DeviceMemory _vkDeviceMemory;
        private ImageView _vkImageView;

        // Public accessors
        public ID3D11Device? D3dDevice => _d3dDevice;
        public ID3D11DeviceContext? D3dContext => _d3dContext;
        public IDXGISwapChain1? SwapChain => _swapChain;
        public ID3D11Texture2D? SharedTexture => _sharedTexture;
        public IntPtr SharedHandle => _sharedHandle;
        public VkImage VkImage => _vkImage;
        public DeviceMemory VkDeviceMemory => _vkDeviceMemory;
        public ImageView VkImageView => _vkImageView;

        public SwapChainManager(VulkanCore core)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));
        }

        /// <summary>
        /// Initialize DirectX 11 and create SwapChain for the panel
        /// </summary>
        public void InitializeDirectX(SwapChainPanel panel, int width, int height)
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
            panelNative.SetSwapChain(_swapChain.NativePointer);

            // 4. Create Shared Texture (The bridge between Vulkan and DX11)
            CreateSharedTexture(width, height);

            dxgiFactory.Dispose();
            dxgiAdapter.Dispose();
            dxgiDevice.Dispose();
        }

        /// <summary>
        /// Create or recreate the shared texture at specified dimensions
        /// </summary>
        public void CreateSharedTexture(int width, int height)
        {
            _sharedTexture?.Dispose();

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

            // Get Shared Handle (NT Handle)
            var resource = _sharedTexture.QueryInterface<IDXGIResource1>();
            _sharedHandle = resource.CreateSharedHandle(null, DxgiSharedResourceFlags.Read | DxgiSharedResourceFlags.Write, null);
            resource.Dispose();
        }

        /// <summary>
        /// Import the shared texture into Vulkan
        /// </summary>
        public void ImportSharedTexture(int width, int height)
        {
            var vk = _core.Vk;
            var device = _core.Device;
            var khrExternalMemory = _core.KhrExternalMemoryWin32;

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

            // Specify that this image can be backed by external memory
            var externalMemoryImageCreateInfo = new ExternalMemoryImageCreateInfo
            {
                SType = StructureType.ExternalMemoryImageCreateInfo,
                HandleTypes = ExternalMemoryHandleTypeFlags.D3D11TextureBit
            };
            imageCreateInfo.PNext = &externalMemoryImageCreateInfo;

            VulkanCore.CheckVkResult(vk.CreateImage(device, in imageCreateInfo, null, out _vkImage));

            // Get Memory Requirements
            vk.GetImageMemoryRequirements(device, _vkImage, out var memReqs);

            // Allocate Memory (Importing)
            var allocInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memReqs.Size,
                PNext = &memoryImportInfo
            };

            // Find a memory type index that supports the D3D11 memory
            var handleProps = new MemoryWin32HandlePropertiesKHR
            {
                SType = StructureType.MemoryWin32HandlePropertiesKhr
            };
            khrExternalMemory!.GetMemoryWin32HandleProperties(device, ExternalMemoryHandleTypeFlags.D3D11TextureBit, _sharedHandle, out handleProps);

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

            VulkanCore.CheckVkResult(vk.AllocateMemory(device, in allocInfo, null, out _vkDeviceMemory));
            VulkanCore.CheckVkResult(vk.BindImageMemory(device, _vkImage, _vkDeviceMemory, 0));

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

            VulkanCore.CheckVkResult(vk.CreateImageView(device, in viewInfo, null, out _vkImageView));
        }

        /// <summary>
        /// Resize the swap chain and shared texture
        /// </summary>
        public void Resize(int width, int height)
        {
            var vk = _core.Vk;
            var device = _core.Device;

            // Dispose old Vulkan image resources
            vk.DestroyImageView(device, _vkImageView, null);
            vk.FreeMemory(device, _vkDeviceMemory, null);
            vk.DestroyImage(device, _vkImage, null);

            // Resize DX11 swapchain
            _swapChain?.ResizeBuffers(2, width, height, DxgiFormat.B8G8R8A8_UNorm, SwapChainFlags.None);

            // Re-create Shared Texture
            CreateSharedTexture(width, height);

            // Re-import to Vulkan
            ImportSharedTexture(width, height);
        }

        /// <summary>
        /// Present the frame using keyed mutex synchronization
        /// </summary>
        public void Present()
        {
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

        private static ISwapChainPanelNative GetSwapChainPanelNative(SwapChainPanel panel)
        {
            IntPtr pointer = Marshal.GetIUnknownForObject(panel);
            if (pointer == IntPtr.Zero)
                throw new InvalidOperationException("Failed to get IUnknown from SwapChainPanel");

            try
            {
                Guid guid = new Guid("63aad0b8-7c24-40ff-85a8-640d944cc325");
                int hr = Marshal.QueryInterface(pointer, ref guid, out IntPtr nativePtr);
                if (hr != 0 || nativePtr == IntPtr.Zero)
                    throw new InvalidOperationException($"Failed to query ISwapChainPanelNative. HRESULT: 0x{hr:X8}");

                return (ISwapChainPanelNative)Marshal.GetObjectForIUnknown(nativePtr);
            }
            finally
            {
                Marshal.Release(pointer);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            var vk = _core.Vk;
            var device = _core.Device;

            vk.DestroyImageView(device, _vkImageView, null);
            vk.FreeMemory(device, _vkDeviceMemory, null);
            vk.DestroyImage(device, _vkImage, null);

            _sharedTexture?.Dispose();
            _swapChain?.Dispose();
            _d3dDevice?.Dispose();

            _disposed = true;
        }
    }
}
