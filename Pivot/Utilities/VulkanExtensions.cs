using Silk.NET.Vulkan;
using System;

namespace Pivot.Utilities
{
    public static class VulkanExtensions
    {
        public static void CheckError(this Result result)
        {
            if (result != Result.Success)
            {
                throw new Exception($"Vulkan Error: {result}");
            }
        }
    }
}
