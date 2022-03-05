using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AbstractVulkan;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using VMASharp;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace TechDemo;

internal struct Vertex
{
	public Vector3 VertPosition;
	public Vector2 VertUV;
	private unsafe fixed float alignment[3];
}

public static unsafe partial class Program
{
	private static readonly IWindow window;

	private static readonly Vk vk;
	private static readonly Instance instance;
	private static readonly KhrSurface khrSurface;

	private static readonly SurfaceKHR surface;
	private static readonly PhysicalDevice physicalDevice;
	private static readonly uint graphicsQueueFamily = uint.MaxValue, computeQueueFamily = uint.MaxValue, presentQueueFamily = uint.MaxValue;
	private static readonly Device device;

	private static readonly uint FramesInFlight;

	static Program()
	{
		/*window = Window.Create(WindowOptions.DefaultVulkan with
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

		

		C(EnableValidation ?
			vk.CreateInstance("TechDemo", new Version32(1, 0, 0), "No Engine", new Version32(0, 0, 0), Vk.Version11, null, new[] { "VK_LAYER_KHRONOS_validation" }, extensions, &debugUtilsMessengerCreateInfoEXT, out instance)
			: vk.CreateInstance("TechDemo", new Version32(1, 0, 0), "No Engine", new Version32(0, 0, 0), Vk.Version11, null, Array.Empty<string>(), extensions, null, out instance)
			);

		// vk.CurrentInstance = instance; // handled by vk.CreateInstance

		if (!vk.TryGetInstanceExtension<KhrSurface>(instance, out khrSurface)) Throw("No KHR_surface");

		surface = window.VkSurface!.Create<AllocationCallbacks>(instance.ToHandle(), null).ToSurface();

		{
			uint physicalDeviceCount;
			C(vk.EnumeratePhysicalDevices(instance, &physicalDeviceCount, null));
			if (physicalDeviceCount is 0) Throw("No devices found");
			var physicalDevices = (PhysicalDevice*)NativeMemory.Alloc((nuint)sizeof(PhysicalDevice) * physicalDeviceCount);
			C(vk.EnumeratePhysicalDevices(instance, &physicalDeviceCount, physicalDevices));

			for (uint pdi = 0; pdi < physicalDeviceCount; pdi++)
			{
				var physicalDevice = physicalDevices[pdi];

				bool queues = false;
				bool extensionsCheck = false;

				{
					uint queueFamilyCount;
					vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &queueFamilyCount, null);
					if (queueFamilyCount is 0) continue;
					var queueFamilyProperties = (QueueFamilyProperties*)NativeMemory.Alloc((nuint)sizeof(QueueFamilyProperties) * queueFamilyCount);
					vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &queueFamilyCount, queueFamilyProperties);
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
					NativeMemory.Free(queueFamilyProperties);
					if (graphicsQueueFamily is not uint.MaxValue && computeQueueFamily is not uint.MaxValue && presentQueueFamily is not uint.MaxValue)
					{
						queues = true;
					}
					else
					{
						graphicsQueueFamily = computeQueueFamily = presentQueueFamily = uint.MaxValue;
					}
				}
				// TODO: other checks besides queue families
				{
					uint extensionCount;
					vk.EnumerateDeviceExtensionProperties(physicalDevice, (byte*)null, &extensionCount, null);
					if (extensionCount is 0) break;
					var extensions = (ExtensionProperties*)NativeMemory.Alloc((nuint)sizeof(ExtensionProperties) * extensionCount);
					string[] extensionNames = new string[extensionCount];
					for (uint extensionId = 0; extensionId < extensionCount; extensionId++)
					{
						extensionNames[extensionId] = Marshal.PtrToStringAnsi((IntPtr)extensions[extensionId].ExtensionName)!;
					}
					NativeMemory.Free(extensions);
					extensionsCheck = true;
					foreach (string extension in Program.extensions)
					{
						//if (!extensionNames.Contains(extension)) extensionsCheck = false;
					}
				}

				if (queues && extensionsCheck) // TODO: other checks
				{
					Program.physicalDevice = physicalDevice;
					break;
				}
			}
			NativeMemory.Free(physicalDevices);

			if (physicalDevice.Handle is 0) Throw("No matching device found");
		}

		{
			uint[] queueFamilies = new[] { graphicsQueueFamily, computeQueueFamily, presentQueueFamily }.Distinct().ToArray();

			var queueCreateInfos = (DeviceQueueCreateInfo*)NativeMemory.Alloc((nuint)(sizeof(DeviceQueueCreateInfo) * queueFamilies.Length));

			float priority = 1.0f;

			for (uint i = 0; i < queueFamilies.Length; i++)
			{
				queueCreateInfos[i] = new()
				{
					SType = StructureType.DeviceQueueCreateInfo,
					PNext = null,
					Flags = 0,
					QueueFamilyIndex = queueFamilies[i],
					QueueCount = 1,
					PQueuePriorities = &priority
				};
			}

			PhysicalDeviceFeatures features = new();

			DeviceCreateInfo deviceCreateInfo = new()
			{
				SType = StructureType.DeviceCreateInfo,
				Flags = 0,
				PNext = null,
				PQueueCreateInfos = queueCreateInfos,
				QueueCreateInfoCount = (uint)queueFamilies.Length,
				PEnabledFeatures = &features,
				EnabledExtensionCount = 0, //(uint)extensions.Count,
				PpEnabledExtensionNames = null, //(byte**)SilkMarshal.StringArrayToPtr(extensions),
				EnabledLayerCount = EnableValidation ? 1 : 0,
				PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(new[] { "VK_LAYER_KHRONOS_validation" })
			};

			Device device;

			C(vk.CreateDevice(physicalDevice, &deviceCreateInfo, null, &device));

			Program.device = device;
			// vk.CurrentDevice = device; // handled by vk.CreateDevice

			//SilkMarshal.Free((nint)deviceCreateInfo.PpEnabledLayerNames);
			SilkMarshal.Free((nint)deviceCreateInfo.PpEnabledExtensionNames);
			NativeMemory.Free(queueCreateInfos);
		}*/
	}
	public static void Main(string[] args)
	{
		TechDemoApplication app = new();
		app.Run();
	}

	struct ManagedBuffer
	{
		Buffer buff;
		Allocation allocation;
	}

	class TechDemoApplication : BaseVulkanApplication
	{
		private readonly SampleCountFlags sampleCount;
		private readonly Format depthImageFormat = Format.D32Sfloat;

		Task uniformLayoutASYNC;

		public readonly DescriptorSetLayout uniformLayout;
		public readonly DescriptorSetLayout textureLayout;

		public readonly DescriptorSet uniformSet;
		public readonly DescriptorSet textureSet;

		private readonly KhrSwapchain khrSwapchain;
		private readonly IWindow window;
		private readonly SurfaceKHR surface;
		private SurfaceCapabilitiesKHR SurfaceCapabilities
		{
			get
			{
				SurfaceCapabilitiesKHR surfaceCapabilities;
				khrSurface.GetPhysicalDeviceSurfaceCapabilities(physicalDevice, surface, &surfaceCapabilities);
				return surfaceCapabilities;
			}
		}
		private readonly SurfaceFormatKHR surfaceFormat;
		private readonly SwapchainKHR swapchain;
		private readonly RenderPass renderPass;

		private readonly uint graphicsQueueIndices;
		private uint* GraphicsQueueIndices
		{ // TODO: WTF
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				uint graphicsQueueIndicesLOCAL = graphicsQueueIndices;
				return &graphicsQueueIndicesLOCAL;
			}
		}
		private readonly Queue graphicsQueue;

		private readonly (Buffer, Allocation) VertexBuffer;

		public TechDemoApplication() : base()
		{
			PhysicalDeviceProperties physicalDeviceProperties;
			vk.GetPhysicalDeviceProperties(physicalDevice, &physicalDeviceProperties);

			sampleCount = (physicalDeviceProperties.Limits.FramebufferColorSampleCounts & physicalDeviceProperties.Limits.FramebufferDepthSampleCounts).GetMaximumSamples();

			FormatProperties formatProperties;
			vk.GetPhysicalDeviceFormatProperties(physicalDevice, Format.D32Sfloat, &formatProperties);

			window = CreateWindow(WindowOptions.DefaultVulkan);

			surface = window.VkSurface!.Create<AllocationCallbacks>(instance.ToHandle(), null).ToSurface();

			vk.TryGetDeviceExtension(instance, device, out khrSwapchain);

			{
				uint surfaceFormatCount;
				khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, surface, &surfaceFormatCount, null);
				var surfaceFormats = (SurfaceFormatKHR*)NativeMemory.Alloc((nuint)sizeof(SurfaceFormatKHR) * surfaceFormatCount);
				khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, surface, &surfaceFormatCount, surfaceFormats);
				for (uint i = 0; i < surfaceFormatCount; i++)
					if (surfaceFormats[i].ColorSpace.HasFlag(ColorSpaceKHR.ColorSpaceSrgbNonlinearKhr) && surfaceFormats[i].Format.HasFlag(Format.B8G8R8A8Srgb))
					{
						surfaceFormat = new(Format.B8G8R8A8Srgb, ColorSpaceKHR.ColorSpaceSrgbNonlinearKhr);
						break;
					}
				NativeMemory.Free(surfaceFormats);
			}

			Extent2D GetExtent(SurfaceCapabilitiesKHR surfaceCapabilities)
			{
				if (surfaceCapabilities.CurrentExtent.Width is not uint.MaxValue && surfaceCapabilities.CurrentExtent.Height is not uint.MaxValue) return surfaceCapabilities.CurrentExtent;
				var framebufferSize = window.FramebufferSize;
				return new(Math.Clamp((uint)framebufferSize.X, surfaceCapabilities.MinImageExtent.Width, surfaceCapabilities.MaxImageExtent.Width), Math.Clamp((uint)framebufferSize.Y, surfaceCapabilities.MinImageExtent.Height, surfaceCapabilities.MaxImageExtent.Height));
			}

			SwapchainCreateInfoKHR swapchainCI = new(
				surface: surface,
				minImageCount: SurfaceCapabilities.MinImageCount,
				imageFormat: surfaceFormat.Format,
				imageColorSpace: surfaceFormat.ColorSpace,
				imageExtent: GetExtent(SurfaceCapabilities),
				imageArrayLayers: 1,
				imageUsage: ImageUsageFlags.ImageUsageColorAttachmentBit,
				preTransform: SurfaceCapabilities.CurrentTransform,
				compositeAlpha: CompositeAlphaFlagsKHR.CompositeAlphaOpaqueBitKhr,
				presentMode: PresentModeKHR.PresentModeImmediateKhr // TODO: vsync
			);

			C(khrSwapchain.CreateSwapchain(device, &swapchainCI, null, out swapchain));

			graphicsQueueIndices = 0; // TODO: actual handling
			graphicsQueue = queues[0]; // TODO: actual handling
			fixed (uint* graphicsQueueIndicesPTR = &graphicsQueueIndices)
			{
				Buffer staging = allocator.CreateBuffer(new(
					size: ((ulong)sizeof(Vertex) * 8),
					usage: BufferUsageFlags.BufferUsageTransferSrcBit,
					sharingMode: SharingMode.Exclusive,
					queueFamilyIndexCount: 1,
					pQueueFamilyIndices: graphicsQueueIndicesPTR), new(
					usage: MemoryUsage.CPU_Only,
					requiredFlags: MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit
				), out Allocation stagingAllocation);

				Vertex* mapped = (Vertex*)stagingAllocation.Map();

				mapped[0] = new()
				{
					VertPosition = new(-1, -1, -1),
					VertUV = new(0, 1)
				};
				mapped[1] = new()
				{
					VertPosition = new(1, -1, -1),
					VertUV = new(1, 1)
				};
				mapped[2] = new()
				{
					VertPosition = new(1, 1, -1),
					VertUV = new(1, 0)
				};
				mapped[3] = new()
				{
					VertPosition = new(1, 1, -1),
					VertUV = new(0, 0)
				};
				for (byte i = 4; i < 8; i++) { mapped[i] = mapped[i - 4]; mapped[i].VertPosition.Z = 1; }

				stagingAllocation.Unmap();

				Buffer vb = allocator.CreateBuffer(new(
					size: ((ulong)sizeof(Vertex) * 8),
					usage: BufferUsageFlags.BufferUsageTransferDstBit | BufferUsageFlags.BufferUsageVertexBufferBit,
					sharingMode: SharingMode.Exclusive,
					queueFamilyIndexCount: 1,
					pQueueFamilyIndices: graphicsQueueIndicesPTR), new(
					usage: MemoryUsage.GPU_Only,
					requiredFlags: MemoryPropertyFlags.MemoryPropertyDeviceLocalBit
				), out Allocation allocation);

				BeginSingleTimeCommands();
				BufferCopy region = new(size: (ulong)sizeof(Vertex) * 8);
				vk.CmdCopyBuffer(SingleTimeCommandBuffer, staging, vb, 1, &region);
				EndSingleTimeCommands();

				vk.DestroyBuffer(device, staging, null);
				allocator.FreeMemory(stagingAllocation);

				VertexBuffer = (vb, allocation);
			}
		}

		#region DescriptorSetLayout
		private DescriptorSetLayout _uniformDescriptorSetLayout;
		private DescriptorSetLayout UniformDescriptorSetLayout
		{
			get
			{
				if (_uniformDescriptorSetLayout.Handle is 0)
				{
					DescriptorSetLayoutBinding uniformBinding = new(binding: 0, descriptorType: DescriptorType.UniformBuffer, descriptorCount: 1, stageFlags: ShaderStageFlags.ShaderStageAllGraphics);
					DescriptorSetLayoutCreateInfo uniformLayoutCI = new(bindingCount: 1, pBindings: &uniformBinding);
					C(vk.CreateDescriptorSetLayout(device, uniformLayoutCI, null, out _uniformDescriptorSetLayout));
				}
				return _uniformDescriptorSetLayout;
			}
			set
			{
				if (_uniformDescriptorSetLayout.Handle is not 0) vk.DestroyDescriptorSetLayout(device, _uniformDescriptorSetLayout, null);
				_uniformDescriptorSetLayout = value;
			}
		}

		private DescriptorSetLayout _textureDescriptorSetLayout;
		private DescriptorSetLayout TextureDescriptorSetLayout
		{
			get
			{
				if (_textureDescriptorSetLayout.Handle is 0)
				{
					DescriptorSetLayoutBinding textureBinding = new(binding: 0, descriptorType: DescriptorType.CombinedImageSampler, descriptorCount: 1, stageFlags: ShaderStageFlags.ShaderStageFragmentBit);
					DescriptorSetLayoutCreateInfo textureLayoutCI = new(bindingCount: 1, pBindings: &textureBinding);
					C(vk.CreateDescriptorSetLayout(device, textureLayoutCI, null, out _textureDescriptorSetLayout));
				}
				return _textureDescriptorSetLayout;
			}
			set
			{
				if (_textureDescriptorSetLayout.Handle is not 0) vk.DestroyDescriptorSetLayout(device, _textureDescriptorSetLayout, null);
				_textureDescriptorSetLayout = value;
			}
		}
		#endregion

		private Sampler _sampler;
		private Sampler Sampler
		{
			get
			{
				if (_sampler.Handle is 0)
				{
					SamplerCreateInfo samplerInfo = new()
					{
						SType = StructureType.SamplerCreateInfo,
						MagFilter = Filter.Linear,
						MinFilter = Filter.Linear,
						AddressModeU = SamplerAddressMode.ClampToEdge,
						AddressModeV = SamplerAddressMode.ClampToEdge,
						AddressModeW = SamplerAddressMode.Repeat,
						AnisotropyEnable = true,
						MaxAnisotropy = 4,
						BorderColor = BorderColor.IntOpaqueBlack,
						UnnormalizedCoordinates = false,
						CompareEnable = false,
						CompareOp = CompareOp.Never,
						MipmapMode = SamplerMipmapMode.Linear,
						MinLod = 0,
						MaxLod = 10,
						MipLodBias = 0,
					};
					C(vk.CreateSampler(device, samplerInfo, null, out _sampler));
				}
				return _sampler;
			}
			set
			{
				if (_sampler.Handle is not 0) vk.DestroySampler(device, _sampler, null);
				_sampler = value;
			}
		}

		private RenderPass _renderPass;
		private RenderPass RenderPass
		{
			get
			{
				if (_renderPass.Handle is 0)
				{
					// TODO: Optional MSAA
					AttachmentDescription colorAttachmentDescription = new(
						format: surfaceFormat.Format,
						samples: sampleCount,
						loadOp: AttachmentLoadOp.Clear,
						storeOp: AttachmentStoreOp.Store,
						stencilLoadOp: AttachmentLoadOp.DontCare,
						stencilStoreOp: AttachmentStoreOp.DontCare,
						initialLayout: ImageLayout.Undefined, // TODO: should it be?
						finalLayout: ImageLayout.ColorAttachmentOptimal
					);
					AttachmentDescription resolveAttachmentDescription = colorAttachmentDescription with
					{
						Samples = SampleCountFlags.SampleCount1Bit,
						FinalLayout = ImageLayout.PresentSrcKhr
					};
					AttachmentDescription depthAttachmentDescription = new(
						format: depthImageFormat,
						samples: sampleCount,
						loadOp: AttachmentLoadOp.Clear,
						storeOp: AttachmentStoreOp.Store,
						stencilLoadOp: AttachmentLoadOp.DontCare,
						stencilStoreOp: AttachmentStoreOp.DontCare,
						initialLayout: ImageLayout.Undefined, // TODO: should it be?
						finalLayout: ImageLayout.DepthStencilAttachmentOptimal
					);

					AttachmentReference colorAttachmentReference = new(0, ImageLayout.ColorAttachmentOptimal);
					AttachmentReference resolveAttachmentReference = new(1, ImageLayout.ColorAttachmentOptimal);
					AttachmentReference depthAttachmentReference = new(2, ImageLayout.DepthStencilAttachmentOptimal);

					SubpassDescription subpassDescription = new(pipelineBindPoint: PipelineBindPoint.Graphics, colorAttachmentCount: 1, pColorAttachments: &colorAttachmentReference, pResolveAttachments: &resolveAttachmentReference, pDepthStencilAttachment: &depthAttachmentReference);

					SubpassDependency subpassDependency = new()
					{
						SrcSubpass = Vk.SubpassExternal,
						DstSubpass = 0,
						SrcStageMask = PipelineStageFlags.PipelineStageBottomOfPipeBit,
						DstStageMask = PipelineStageFlags.PipelineStageColorAttachmentOutputBit | PipelineStageFlags.PipelineStageEarlyFragmentTestsBit,
						SrcAccessMask = 0,
						DstAccessMask = AccessFlags.AccessColorAttachmentWriteBit | AccessFlags.AccessDepthStencilAttachmentWriteBit
					};

					AttachmentDescription* attachmentDescriptions = stackalloc[] { colorAttachmentDescription, resolveAttachmentDescription, depthAttachmentDescription };

					RenderPassCreateInfo renderPassCI = new(attachmentCount: 3, pAttachments: attachmentDescriptions, subpassCount: 1, pSubpasses: &subpassDescription, dependencyCount: 1, pDependencies: &subpassDependency);

					C(vk.CreateRenderPass(device, &renderPassCI, null, out _renderPass));
				}
				return _renderPass;
			}
			set
			{
				if (_renderPass.Handle is not 0) vk.DestroyRenderPass(device, _renderPass, null);
				_renderPass = value;
			}
		}

		private PipelineLayout _pipelineLayout;
		private PipelineLayout PipelineLayout
		{
			get
			{
				if (_pipelineLayout.Handle is 0)
				{
					DescriptorSetLayout* descriptorSetLayouts = stackalloc[] { UniformDescriptorSetLayout, TextureDescriptorSetLayout };
					PipelineLayoutCreateInfo pipelineLayoutCI = new(
						setLayoutCount: 2,
						pSetLayouts: descriptorSetLayouts
					);
					C(vk.CreatePipelineLayout(device, &pipelineLayoutCI, null, out _pipelineLayout));
				}
				return _pipelineLayout;
			}
			set
			{
				if (_pipelineLayout.Handle is not 0) vk.DestroyPipelineLayout(device, _pipelineLayout, null);
				_pipelineLayout = value;
			}
		}

		private byte* _main;
		private byte* Main
		{
			get
			{
				if (_main is null) _main = (byte*)SilkMarshal.StringToPtr("main");
				return _main;
			}
			set
			{
				if (_main is not null) SilkMarshal.Free((nint)_main);
				_main = value;
			}
		}

		private ShaderModule CreateShaderModuleFromEmbeddedFile(string name)
		{
			using var stream = typeof(TechDemoApplication).Assembly.GetManifestResourceStream("TechDemo.shaders.compiled." + name + ".spv")!;
			var len = (nuint)stream.Length;
			var bytes = (byte*)NativeMemory.Alloc(len);
			stream.Read(new Span<byte>(bytes, (int)len));
			stream.Close();
			stream.Dispose();
			C(vk.CreateShaderModule(device, bytes, len, out ShaderModule module));
			NativeMemory.Free(bytes);
			return module;
		}

		private ShaderModule _vertexShaderModule;
		private ShaderModule VertexShaderModule
		{
			get
			{
				if (_vertexShaderModule.Handle is 0)
					_vertexShaderModule = CreateShaderModuleFromEmbeddedFile("main.vert");
				return _vertexShaderModule;
			}
			set
			{
				if (_vertexShaderModule.Handle is not 0) vk.DestroyShaderModule(device, _vertexShaderModule, null);
				_vertexShaderModule = value;
			}
		}

		private ShaderModule _fragmentShaderModule;
		private ShaderModule FragmentShaderModule
		{
			get
			{
				if (_fragmentShaderModule.Handle is 0)
					_fragmentShaderModule = CreateShaderModuleFromEmbeddedFile("main.frag");
				return _fragmentShaderModule;
			}
			set
			{
				if (_fragmentShaderModule.Handle is not 0) vk.DestroyShaderModule(device, _fragmentShaderModule, null);
				_fragmentShaderModule = value;
			}
		}

		private Pipeline _pipeline;
		private Pipeline Pipeline
		{
			get
			{
				if (_pipeline.Handle is 0)
				{
					VertexInputBindingDescription vertexInputBindingDescription = new()
					{
						Binding = 0,
						Stride = sizeof(float) * 6,
						InputRate = VertexInputRate.Vertex
					};
					VertexInputAttributeDescription* vertexInputAttributeDescriptions = stackalloc VertexInputAttributeDescription[] {
						new(0, 0, Format.R32G32B32A32Sfloat, 0),
						new(1, 0, Format.R32G32B32A32Sfloat, sizeof(float) * 3)
					};
					PipelineVertexInputStateCreateInfo pipelineVertexInputStateCI = new(
						vertexBindingDescriptionCount: 1,
						pVertexBindingDescriptions: &vertexInputBindingDescription,
						vertexAttributeDescriptionCount: 2,
						pVertexAttributeDescriptions: vertexInputAttributeDescriptions
					);
					PipelineInputAssemblyStateCreateInfo pipelineInputAssemblyStateCI = new(topology: PrimitiveTopology.TriangleList);
					PipelineViewportStateCreateInfo pipelineViewportStateCI = new(viewportCount: 1);
					PipelineRasterizationStateCreateInfo pipelineRasterizationStateCI = new(
						rasterizerDiscardEnable: Vk.True,
						polygonMode: PolygonMode.Fill,
						cullMode: CullModeFlags.CullModeBackBit,
						frontFace: FrontFace.CounterClockwise,
						lineWidth: 1.0f
					);
					PipelineMultisampleStateCreateInfo pipelineMultisampleStateCI = new(rasterizationSamples: sampleCount);
					PipelineDepthStencilStateCreateInfo pipelineDepthStencilStateCI = new(
						depthTestEnable: Vk.True,
						depthWriteEnable: Vk.True,
						depthCompareOp: CompareOp.Less,
						depthBoundsTestEnable: Vk.True,
						minDepthBounds: 0.0f,
						maxDepthBounds: 1.0f
					);
					PipelineColorBlendAttachmentState pipelineColorBlendAttachmentState = new(
						blendEnable: Vk.True,
						srcColorBlendFactor: BlendFactor.One,
						dstColorBlendFactor: BlendFactor.One,
						colorBlendOp: BlendOp.Add,
						srcAlphaBlendFactor: BlendFactor.One,
						dstAlphaBlendFactor: BlendFactor.One,
						alphaBlendOp: BlendOp.Add,
						colorWriteMask: ColorComponentFlags.ColorComponentRBit | ColorComponentFlags.ColorComponentGBit | ColorComponentFlags.ColorComponentBBit | ColorComponentFlags.ColorComponentABit
					);
					PipelineColorBlendStateCreateInfo pipelineColorBlendStateCI = new(
						logicOpEnable: Vk.True,
						logicOp: LogicOp.And,
						attachmentCount: 1,
						pAttachments: &pipelineColorBlendAttachmentState
					);
					new Span<float>(pipelineColorBlendStateCI.BlendConstants, 4).Fill(1);
					DynamicState dynamicState = DynamicState.Viewport;
					PipelineDynamicStateCreateInfo pipelineDynamicStateCI = new(dynamicStateCount: 1, pDynamicStates: &dynamicState);
					PipelineShaderStageCreateInfo* pipelineShaderStageCIs = stackalloc PipelineShaderStageCreateInfo[] {
						new(module: VertexShaderModule, stage: ShaderStageFlags.ShaderStageVertexBit, pName: Main),
						new(module: FragmentShaderModule, stage: ShaderStageFlags.ShaderStageFragmentBit, pName: Main)
					};
					GraphicsPipelineCreateInfo graphicsPipelineCI = new(
						stageCount: 2,
						pStages: pipelineShaderStageCIs,
						pVertexInputState: &pipelineVertexInputStateCI,
						pInputAssemblyState: &pipelineInputAssemblyStateCI,
						pRasterizationState: &pipelineRasterizationStateCI,
						pMultisampleState: &pipelineMultisampleStateCI,
						pDepthStencilState: &pipelineDepthStencilStateCI,
						pColorBlendState: &pipelineColorBlendStateCI,
						pDynamicState: &pipelineDynamicStateCI,
						layout: PipelineLayout,
						renderPass: RenderPass
					);

					// TODO: PipelineCache
					C(vk.CreateGraphicsPipelines(device, default, 1, &graphicsPipelineCI, null, out _pipeline));
				}
				return _pipeline;
			}
			set
			{
				if (_pipeline.Handle is not 0) vk.DestroyPipeline(device, _pipeline, null);
				_pipeline = value;
			}
		}

		private CommandPool _commandPool;
		private CommandPool CommandPool
		{
			get
			{
				if (_commandPool.Handle is 0)
				{
					CommandPoolCreateInfo commandPoolCI = new(queueFamilyIndex: graphicsQueueIndices);
					C(vk.CreateCommandPool(device, &commandPoolCI, null, out _commandPool));
				}
				return _commandPool;
			}
			set
			{
				if (_commandPool.Handle is not 0) vk.DestroyCommandPool(device, _commandPool, null);
				_commandPool = value;
			}
		}

		private Fence _singleTimeFence = default;
		private Fence SingleTimeFence
		{
			get
			{
				if (_singleTimeFence.Handle is 0)
				{
					FenceCreateInfo fenceCI = new() { SType = StructureType.FenceCreateInfo };
					C(vk.CreateFence(device, &fenceCI, null, out _singleTimeFence));
				}
				return _singleTimeFence;
			}
			set
			{
				if (_singleTimeFence.Handle is not 0) vk.DestroyFence(device, _singleTimeFence, null);
				_singleTimeFence = value;
			}
		}

		private CommandBuffer _singleTimeCommandBuffer = default;
		private CommandBuffer SingleTimeCommandBuffer
		{
			get
			{
				if (_singleTimeCommandBuffer.Handle is 0)
				{
					CommandBufferAllocateInfo commandBufferAllocateInfo = new(commandPool: CommandPool, commandBufferCount: 1);
					vk.AllocateCommandBuffers(device, &commandBufferAllocateInfo, out _singleTimeCommandBuffer);
				}
				return _singleTimeCommandBuffer;
			}
			set
			{
				if (_singleTimeCommandBuffer.Handle is not 0) vk.FreeCommandBuffers(device, CommandPool, 1, _singleTimeCommandBuffer);
				_singleTimeCommandBuffer = value;
			}
		}

		private void BeginSingleTimeCommands()
		{
			CommandBufferBeginInfo commandBufferBeginInfo = new() { SType = StructureType.CommandBufferBeginInfo };
			C(vk.BeginCommandBuffer(SingleTimeCommandBuffer, &commandBufferBeginInfo));
		}
		private void EndSingleTimeCommands()
		{
			var SingleTimeCommandBufferLOCAL = SingleTimeCommandBuffer;
			vk.EndCommandBuffer(SingleTimeCommandBufferLOCAL);
			Fence SingleTimeFenceLOCAL = SingleTimeFence;
			SubmitInfo submitInfo = new(commandBufferCount: 1, pCommandBuffers: &SingleTimeCommandBufferLOCAL);
			C(vk.QueueSubmit(graphicsQueue, 1, &submitInfo, SingleTimeFenceLOCAL));
			C(vk.WaitForFences(device, 1, SingleTimeFenceLOCAL, Vk.True, 0));
			vk.ResetFences(device, 1, &SingleTimeFenceLOCAL);
		}

		private void CreateTexture()
		{
			uint width = 512;
			uint height = 512;

			Buffer stagingBuffer = allocator.CreateBuffer(new(size: width * height * 4, usage: BufferUsageFlags.BufferUsageTransferSrcBit, queueFamilyIndexCount: 1, pQueueFamilyIndices: GraphicsQueueIndices), new(usage: MemoryUsage.CPU_To_GPU, requiredFlags: MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit), out Allocation stagingAllocation);
			byte* mapped = (byte*)stagingAllocation.Map();
			nuint address = 0;
			for (nuint pixel = 0; pixel < width * height; pixel++)
			{
				mapped[address] = (byte)(pixel / width * height);
				mapped[address + 3] = byte.MaxValue;
				address += 3;
			}
			stagingAllocation.Unmap();

			ImageCreateInfo imageCI = new(
				imageType: ImageType.ImageType2D,
				format: Format.B8G8R8A8Srgb,
				extent: new(width, height),
				mipLevels: (uint)MathF.Ceiling(MathF.Log2(MathF.Max(width, height))),
				arrayLayers: 1,
				samples: SampleCountFlags.SampleCount1Bit,
				usage: ImageUsageFlags.ImageUsageTransferDstBit | ImageUsageFlags.ImageUsageSampledBit,
				queueFamilyIndexCount: 1,
				pQueueFamilyIndices: GraphicsQueueIndices
			);
			Image image = allocator.CreateImage(imageCI, new(usage: MemoryUsage.GPU_Only, requiredFlags: MemoryPropertyFlags.MemoryPropertyDeviceLocalBit), out Allocation textureAllocation);

			BeginSingleTimeCommands();
			BufferImageCopy bufferImageCopy = new(bufferRowLength: width * 4, bufferImageHeight: height, imageSubresource: new ImageSubresourceLayers(ImageAspectFlags.ImageAspectColorBit, 0, 0, 1), imageExtent: new(width, height, 1));
			vk.CmdCopyBufferToImage(SingleTimeCommandBuffer, stagingBuffer, image, ImageLayout.TransferDstOptimal, 1, &bufferImageCopy);
			EndSingleTimeCommands();
		}

		public void CreatePipelines()
		{

		}
		public void SyncSwapchain()
		{

		}
		public void Run()
		{

		}
	}
}
