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
	private static readonly uint graphicsFamily = uint.MaxValue, computeFamily = uint.MaxValue, presentFamily = uint.MaxValue;

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
			uint deviceCount;
			vk.EnumeratePhysicalDevices(instance, &deviceCount, null);
			if(deviceCount is 0) Throw("No devices found");
			var physicalDevices = GC.AllocateUninitializedArray<PhysicalDevice>((int)deviceCount);
			fixed(PhysicalDevice* physicalDevicesPtr = physicalDevices){
				vk.EnumeratePhysicalDevices(instance, &deviceCount, physicalDevicesPtr);
			}
			
			foreach(var physicalDevice in physicalDevices){
				uint queueFamilyCount;
				vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &queueFamilyCount, null);
				if(queueFamilyCount is 0) continue;
				var queueFamilyProperties = GC.AllocateUninitializedArray<QueueFamilyProperties>((int)queueFamilyCount);
				fixed(QueueFamilyProperties* queueFamilyPropertiesPtr = queueFamilyProperties){
					vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &queueFamilyCount, queueFamilyPropertiesPtr);
				}
				for(uint i = 0; i< queueFamilyCount; i++){
					QueueFamilyProperties iQueueFamilyProperties = queueFamilyProperties[i];
					if(iQueueFamilyProperties.QueueFlags.HasFlag(QueueFlags.QueueGraphicsBit)) if(graphicsFamily is uint.MaxValue) graphicsFamily = i;
					if(iQueueFamilyProperties.QueueFlags.HasFlag(QueueFlags.QueueComputeBit)) if(computeFamily is uint.MaxValue) computeFamily = i;
					if(presentFamily is uint.MaxValue){
						Bool32 supported;
						khrSurface.GetPhysicalDeviceSurfaceSupport(physicalDevice, i, surface, &supported);
						if(supported.Value is 1) presentFamily = i;
					}
					if(graphicsFamily is not uint.MaxValue && computeFamily is not uint.MaxValue && presentFamily is not uint.MaxValue) break;
				}
				if(graphicsFamily is not uint.MaxValue && computeFamily is not uint.MaxValue && presentFamily is not uint.MaxValue){
					Program.physicalDevice = physicalDevice;
				}
				else{
					graphicsFamily = computeFamily = presentFamily = uint.MaxValue;
				}
				// TODO: other checks besides queue families
			}
			if(graphicsFamily is uint.MaxValue || computeFamily is uint.MaxValue || presentFamily is uint.MaxValue) Throw("No device with [ Graphics, Compute, Present ] queue families");
		}
	}
	public static void Main()
	{

	}
}
