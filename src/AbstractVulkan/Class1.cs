using System.Runtime.CompilerServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace AbstractVulkan;

[SkipLocalsInit]
public static unsafe class StartupUtilities
{
	public static Result CreateInstance(this Vk vk, string applicationName, Version32 applicationVersion, string engineName, Version32 engineVersion, Version32 apiVersion, AllocationCallbacks* allocator, IReadOnlyList<string> layers, IReadOnlyList<string> extensions, void* next, out Instance instance)
	{
		ApplicationInfo applicationInfo = new()
		{
			SType = StructureType.ApplicationInfo,
			PApplicationName = (byte*)SilkMarshal.StringToPtr(applicationName, NativeStringEncoding.UTF8),
			ApplicationVersion = applicationVersion,
			PEngineName = (byte*)SilkMarshal.StringToPtr(engineName, NativeStringEncoding.UTF8),
			EngineVersion = engineVersion,
			ApiVersion = apiVersion,
			PNext = null
		};

		InstanceCreateInfo instanceCreateInfo = new()
		{
			SType = StructureType.InstanceCreateInfo,
			PApplicationInfo = &applicationInfo,
			Flags = 0, // TBD
			EnabledLayerCount = (uint)layers.Count,
			PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(layers),
			EnabledExtensionCount = (uint)extensions.Count,
			PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(extensions),
			PNext = next
		};

		Result result;

		fixed (Instance* instancePointer = &instance)
			result = vk.CreateInstance(&instanceCreateInfo, allocator, instancePointer);

		SilkMarshal.Free((nint)applicationInfo.PApplicationName);
		SilkMarshal.Free((nint)applicationInfo.PEngineName);

		return result;
	}
	public static Result CreateInstance<TNext>(this Vk vk, string applicationName, Version32 applicationVersion, string engineName, Version32 engineVersion, Version32 apiVersion, AllocationCallbacks* allocator, IReadOnlyList<string> layers, IReadOnlyList<string> extensions, ref TNext next, out Instance instance) where TNext : unmanaged, IExtendsChain<InstanceCreateInfo>
	{
		return vk.CreateInstance(applicationName, applicationVersion, engineName, engineVersion, apiVersion, allocator, layers, extensions, Unsafe.AsPointer(ref next), out instance);
	}
}
