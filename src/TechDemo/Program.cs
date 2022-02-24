using System.Runtime.InteropServices;
using AbstractVulkan;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace TechDemo;

public static unsafe partial class Program
{
	private static readonly IWindow window;

	private static readonly Vk vk;
	private static readonly Instance instance;
	private static readonly KhrSurface khrSurface;

	private static readonly SurfaceKHR surface;
	private static readonly PhysicalDevice physicalDevice;
	private static readonly uint graphicsQueueFamily = uint.MaxValue, computeQueueFamily = uint.MaxValue, presentQueueFamily = uint.MaxValue;

	private static readonly uint FramesInFlight = 1;

	static Program()
	{
		window = Window.Create(WindowOptions.DefaultVulkan with
		{
			FramesPerSecond = 0,
			UpdatesPerSecond = 0,
			VSync = false,
			IsEventDriven = false,
			Title = "TechDemo"
		});

		window.Initialize();

		if (window.VkSurface is null) throw new Exception("window.VkSurface is null");

		vk = Vk.GetApi();

		{
			byte** windowExtensions = window.VkSurface.GetRequiredExtensions(out uint count);
			for (uint i = 0; i < count; i++) extensions.Add(Marshal.PtrToStringAnsi((nint)windowExtensions[i])!);
		}

		DebugUtilsMessengerCreateInfoEXT debugUtilsMessengerCreateInfoEXT = new()
		{
			SType = StructureType.DebugUtilsMessengerCreateInfoExt,
			PNext = null,
			Flags = 0,
			MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityVerboseBitExt | DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityWarningBitExt | DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityErrorBitExt | DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityInfoBitExt,
			MessageType = DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypeGeneralBitExt | DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypePerformanceBitExt | DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypeValidationBitExt,
			PUserData = null,
			PfnUserCallback = (delegate* unmanaged[Cdecl]<DebugUtilsMessageSeverityFlagsEXT, DebugUtilsMessageTypeFlagsEXT, DebugUtilsMessengerCallbackDataEXT*, void*, Bool32>)&DebugCallback
		};

		C(EnableValidation ?
			vk.CreateInstance("TechDemo", new Version32(1, 0, 0), "No Engine", new Version32(0, 0, 0), Vk.Version11, null, new[] { "VK_LAYER_KHRONOS_validation" }, extensions, &debugUtilsMessengerCreateInfoEXT, out instance)
			: vk.CreateInstance("TechDemo", new Version32(1, 0, 0), "No Engine", new Version32(0, 0, 0), Vk.Version11, null, Array.Empty<string>(), extensions, null, out instance)
			);

		if (!vk.TryGetInstanceExtension<KhrSurface>(instance, out khrSurface)) Throw("No KHR_surface");

		surface = window.VkSurface!.Create<AllocationCallbacks>(instance.ToHandle(), null).ToSurface();

		{
			uint physicalDeviceCount;
			vk.EnumeratePhysicalDevices(instance, &physicalDeviceCount, null);
			if (physicalDeviceCount is 0) Throw("No devices found");
			var physicalDevices = GC.AllocateUninitializedArray<PhysicalDevice>((int)physicalDeviceCount);
			fixed (PhysicalDevice* physicalDevicesPtr = physicalDevices)
			{
				vk.EnumeratePhysicalDevices(instance, &physicalDeviceCount, physicalDevicesPtr);
			}

			foreach (var physicalDevice in physicalDevices)
			{
				uint queueFamilyCount;
				vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &queueFamilyCount, null);
				if (queueFamilyCount is 0) continue;
				var queueFamilyProperties = GC.AllocateUninitializedArray<QueueFamilyProperties>((int)queueFamilyCount);
				fixed (QueueFamilyProperties* queueFamilyPropertiesPtr = queueFamilyProperties)
				{
					vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &queueFamilyCount, queueFamilyPropertiesPtr);
				}
				for (uint i = 0; i < queueFamilyCount; i++)
				{
					QueueFamilyProperties iQueueFamilyProperties = queueFamilyProperties[i];
					if (iQueueFamilyProperties.QueueFlags.HasFlag(QueueFlags.QueueGraphicsBit)) if (graphicsQueueFamily is uint.MaxValue) graphicsQueueFamily = i;
					if (iQueueFamilyProperties.QueueFlags.HasFlag(QueueFlags.QueueComputeBit)) if (computeQueueFamily is uint.MaxValue) computeQueueFamily = i;
					if (presentQueueFamily is uint.MaxValue)
					{
						Bool32 supported;
						khrSurface.GetPhysicalDeviceSurfaceSupport(physicalDevice, i, surface, &supported);
						if (supported.Value is 1) presentQueueFamily = i;
					}
					if (graphicsQueueFamily is not uint.MaxValue && computeQueueFamily is not uint.MaxValue && presentQueueFamily is not uint.MaxValue) break;
				}
				if (graphicsQueueFamily is not uint.MaxValue && computeQueueFamily is not uint.MaxValue && presentQueueFamily is not uint.MaxValue)
				{
					Program.physicalDevice = physicalDevice;
				}
				else
				{
					graphicsQueueFamily = computeQueueFamily = presentQueueFamily = uint.MaxValue;
					continue;
				}
				// TODO: other checks besides queue families
			}
			if (physicalDevice.Handle is 0) Throw("No matching device found");
		}
	}
	public static void Main()
	{

	}
}
