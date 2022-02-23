using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;


namespace TechDemo;
// See https://aka.ms/new-console-template for more information

public static class Program
{
	private const bool EnableValidation = true;
	private static readonly IWindow window;
	private static readonly Vk vk;

	static Program(){
		window = Window.Create(WindowOptions.DefaultVulkan with
		{
			FramesPerSecond = 0,
			UpdatesPerSecond = 0,
			VSync = false,
			IsEventDriven = false,
			Title = "TechDemo"
		});

		if(window.VkSurface is null) throw new Exception("window.VkSurface is null");


	}
	public static void Main()
	{

	}
}
