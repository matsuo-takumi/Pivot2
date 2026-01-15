using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Pivot.Utilities
{
    /// <summary>
    /// Manages core Vulkan resources: Instance, Physical Device, Logical Device, and Queue.
    /// </summary>
    public unsafe class VulkanCore : IDisposable
    {
        private bool _disposed;

        // Vulkan Core
        private readonly Vk _vk;
        private Instance _instance;
        private PhysicalDevice _physicalDevice;
        private Device _device;
        private Queue _graphicsQueue;

        // Vulkan Extensions
        private KhrExternalMemoryWin32? _khrExternalMemoryWin32;
        private KhrWin32Surface? _khrWin32Surface;

        // Public accessors for use by other modules
        public Vk Vk => _vk;
        public Instance Instance => _instance;
        public PhysicalDevice PhysicalDevice => _physicalDevice;
        public Device Device => _device;
        public Queue GraphicsQueue => _graphicsQueue;
        public KhrExternalMemoryWin32? KhrExternalMemoryWin32 => _khrExternalMemoryWin32;
        public KhrWin32Surface? KhrWin32Surface => _khrWin32Surface;

        public VulkanCore()
        {
            _vk = Vk.GetApi();
        }

        /// <summary>
        /// Initialize Vulkan core components: Instance, Physical Device, Logical Device, Queue
        /// </summary>
        public void Initialize()
        {
            CreateInstance();
            PickPhysicalDevice();
            CreateLogicalDevice();
        }

        private void CreateInstance()
        {
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
                System.Diagnostics.Debug.WriteLine("VK_KHR_win32_surface not found");
            }
        }

        private void PickPhysicalDevice()
        {
            uint deviceCount = 0;
            _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, null);
            var devices = new PhysicalDevice[deviceCount];
            fixed (PhysicalDevice* pDevices = devices)
            {
                _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, pDevices);
            }
            _physicalDevice = devices[0]; // Pick the first device for now
        }

        private void CreateLogicalDevice()
        {
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
                QueueFamilyIndex = 0, // Assume graphics queue is 0 (should query)
                QueueCount = 1,
                PQueuePriorities = &queuePriority
            };

            var features = new PhysicalDeviceFeatures
            {
                FillModeNonSolid = true
            };

            var deviceCreateInfo = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &queueCreateInfo,
                EnabledExtensionCount = (uint)deviceExtensions.Count,
                PpEnabledExtensionNames = ppDeviceExtensions,
                PEnabledFeatures = &features
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

        /// <summary>
        /// Find a suitable memory type based on type filter and required properties
        /// </summary>
        public uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
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

        /// <summary>
        /// Wait for device to become idle
        /// </summary>
        public void WaitIdle()
        {
            _vk.DeviceWaitIdle(_device);
        }

        /// <summary>
        /// Helper method to check Vulkan Result and throw if not successful
        /// </summary>
        public static void CheckVkResult(Result result)
        {
            if (result != Result.Success)
            {
                throw new Exception($"Vulkan error: {result}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _vk.DestroyDevice(_device, null);
            _vk.DestroyInstance(_instance, null);

            _disposed = true;
        }
    }
}
