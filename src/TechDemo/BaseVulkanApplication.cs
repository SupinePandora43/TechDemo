using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AbstractVulkan;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using VMASharp;

namespace TechDemo;

public unsafe class BaseVulkanApplication
{
	public readonly Vk vk;
	public readonly Instance instance;
	public readonly KhrSurface khrSurface; // TODO: make optional?
	public readonly PhysicalDevice physicalDevice;
	public readonly Device device;

	public VulkanMemoryAllocator allocator;

	public readonly IReadOnlyList<string> extensions;

	public readonly QueueFamilyProperties[] queueFamilies;
	public readonly IReadOnlyList<Queue> queues;

	public BaseVulkanApplication(bool validation = true, uint frames = 3, uint deviceID = 0)
	{
		vk = Vk.GetApi();

		{
			uint propertyCount = 0;
			vk.EnumerateInstanceExtensionProperties((byte*)null, &propertyCount, null);
			var instanceExtensionProperties = (ExtensionProperties*)NativeMemory.Alloc((nuint)sizeof(ExtensionProperties) * propertyCount);
			vk.EnumerateInstanceExtensionProperties((byte*)null, &propertyCount, instanceExtensionProperties);
			List<string> instanceExtensionNames = new((int)propertyCount);
			for (int i = 0; i < propertyCount; i++)
			{
				instanceExtensionNames.Add(Marshal.PtrToStringAnsi((nint)instanceExtensionProperties[i].ExtensionName)!);
			}
			extensions = instanceExtensionNames.AsReadOnly();
			NativeMemory.Free(instanceExtensionProperties);
		}

		void* instanceCreateInfoNext;
		if (validation)
		{
			DebugUtilsMessengerCreateInfoEXT debugUtilsMessengerCreateInfoEXT = new()
			{
				SType = StructureType.DebugUtilsMessengerCreateInfoExt,
				MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityVerboseBitExt | DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityWarningBitExt | DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityErrorBitExt | DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityInfoBitExt,
				MessageType = DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypeGeneralBitExt | DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypePerformanceBitExt | DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypeValidationBitExt,
				PfnUserCallback = (delegate* unmanaged[Cdecl]<DebugUtilsMessageSeverityFlagsEXT, DebugUtilsMessageTypeFlagsEXT, DebugUtilsMessengerCallbackDataEXT*, void*, Bool32>)&DebugCallback
			};
			instanceCreateInfoNext = &debugUtilsMessengerCreateInfoEXT;
		}
		else instanceCreateInfoNext = null;

		vk.CreateInstance("BaseVulkanApplication", new Version32(1, 0, 0), "No Engine", new Version32(1, 0, 0), Vk.Version11, null, validation ? new[] { "VK_LAYER_KHRONOS_validation" } : Array.Empty<string>(), extensions, instanceCreateInfoNext, out instance);

		if (!vk.TryGetInstanceExtension(instance, out khrSurface)) Throw("Failed to aquire KhrSurface from instance");

		{
			uint physicalDeviceCount;
			C(vk.EnumeratePhysicalDevices(instance, &physicalDeviceCount, null));
			if (physicalDeviceCount is 0) Throw("No devices found");
			var physicalDevices = (PhysicalDevice*)NativeMemory.Alloc((nuint)sizeof(PhysicalDevice) * physicalDeviceCount);
			C(vk.EnumeratePhysicalDevices(instance, &physicalDeviceCount, physicalDevices));
			if (deviceID > physicalDeviceCount) throw new ArgumentOutOfRangeException(nameof(deviceID), "deviceID > physical device count");
			physicalDevice = physicalDevices[deviceID];
			NativeMemory.Free(physicalDevices);

			uint queueFamilyCount;
			vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &queueFamilyCount, null);
			if (queueFamilyCount is 0) Throw("Device doesn't have queues");

			queueFamilies = GC.AllocateUninitializedArray<QueueFamilyProperties>((int)queueFamilyCount);
			fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
				vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &queueFamilyCount, queueFamiliesPtr);

			{
				var queueCreateInfos = (DeviceQueueCreateInfo*)NativeMemory.Alloc((nuint)(sizeof(DeviceQueueCreateInfo) * queueFamilies.Length));

				float priority = 1.0f;

				for (uint i = 0; i < queueFamilies.Length; i++)
				{
					queueCreateInfos[i] = new()
					{
						SType = StructureType.DeviceQueueCreateInfo,
						QueueFamilyIndex = i,
						QueueCount = 1,
						PQueuePriorities = &priority
					};
				}

				PhysicalDeviceFeatures features = new(samplerAnisotropy: true);

				uint extensionCount; // TODO: only required ones
				vk.EnumerateDeviceExtensionProperties(physicalDevice, (byte*)null, &extensionCount, null);
				var deviceExtensions = (ExtensionProperties*)NativeMemory.Alloc((nuint)sizeof(ExtensionProperties) * extensionCount);
				vk.EnumerateDeviceExtensionProperties(physicalDevice, (byte*)null, &extensionCount, deviceExtensions);
				var extensionNames = (byte**)NativeMemory.Alloc((nuint)sizeof(byte*) * extensionCount);
				for (uint i = 0; i < extensionCount; i++)
				{
					extensionNames[i] = deviceExtensions[i].ExtensionName;
				}
				DeviceCreateInfo deviceCreateInfo = new()
				{
					SType = StructureType.DeviceCreateInfo,
					PQueueCreateInfos = queueCreateInfos,
					QueueCreateInfoCount = (uint)queueFamilies.Length,
					PEnabledFeatures = &features,
					EnabledExtensionCount = extensionCount,
					PpEnabledExtensionNames = extensionNames,
					EnabledLayerCount = validation ? 1u : 0u,
					PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(new[] { "VK_LAYER_KHRONOS_validation" })
				};

				Device device;

				C(vk.CreateDevice(physicalDevice, &deviceCreateInfo, null, &device));
				NativeMemory.Free(extensionNames);
				NativeMemory.Free(deviceExtensions);
				NativeMemory.Free(queueCreateInfos);
				SilkMarshal.Free((nint)deviceCreateInfo.PpEnabledLayerNames);

				this.device = device;

				List<Queue> queuesList = new(queueFamilies.Length);
				for (uint i = 0; i < queueFamilies.Length; i++)
				{
					Queue q;
					vk.GetDeviceQueue(device, i, 0, &q);
					queuesList.Add(q);
				}
				queues = queuesList.AsReadOnly();
			}
		}

		allocator = new(new()
		{
			FrameInUseCount = (int)frames,
			VulkanAPIObject = vk,
			VulkanAPIVersion = Vk.Version11,
			Instance = instance,
			PhysicalDevice = physicalDevice,
			LogicalDevice = device
		});
	}

	public IWindow CreateWindow(WindowOptions options)
	{
		var window = Window.Create(WindowOptions.DefaultVulkan with
		{
			FramesPerSecond = 0,
			UpdatesPerSecond = 0,
			VSync = false,
			IsEventDriven = false,
			Title = "TechDemo",
		});
		window.Initialize();
		if (window.VkSurface is null) throw new PlatformNotSupportedException("window.VkSurface is null");
		return window;
	}
	public Task<IWindow> CreateWindowAsync(WindowOptions options)
	{
		if (!OperatingSystem.IsMacOS())
			return Task.Run(() => CreateWindow(options));
		return Task.FromResult(CreateWindow(options));
	}

	static void Throw() => throw new Exception(Environment.StackTrace);
	static void Throw(string cause) => throw new Exception(cause);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void C(Result r)
	{
		if (r is not Result.Success) Throw();
	}

	[UnmanagedCallersOnly(CallConvs = new Type[] { typeof(CallConvCdecl) })]
	private static Bool32 DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity, DebugUtilsMessageTypeFlagsEXT messageTypes, DebugUtilsMessengerCallbackDataEXT* pCallbackData, void* pUserData)
	{
		static void N() { };
		var message = Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage);
		if (message!.StartsWith("Validation Warning:"))
			N();
		if (message!.StartsWith("Validation Error:"))
			N();
		System.Diagnostics.Debug.WriteLine($"Validation:" + message);

		return Vk.False;
	}
}