using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Vulkan;

namespace TechDemo;

unsafe partial class Program
{
	private const bool EnableValidation = true;

	private static readonly List<string> extensions = new string[]{}.ToList();

	[UnmanagedCallersOnly(CallConvs = new Type[] { typeof(CallConvCdecl) })]
	private static Bool32 DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity, DebugUtilsMessageTypeFlagsEXT messageTypes, DebugUtilsMessengerCallbackDataEXT* pCallbackData, void* pUserData)
	{
		static void N() { };
		var message = Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage);
		if (message!.StartsWith("Validation Warning:"))
			N();
		if (message!.StartsWith("Validation Error:"))
			N();
		System.Diagnostics.Debug.WriteLine($"validation layer:" + message);

		return Vk.False;
	}

	static void Throw() => throw new Exception(Environment.StackTrace);
	static void Throw(string cause) => throw new Exception(cause);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void C(Result r){
		if(r is not Result.Success) Throw();
	}
}
