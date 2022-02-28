using Silk.NET.Vulkan;

namespace AbstractVulkan;

public static class Redundancy
{
	public static SampleCountFlags GetMaximumSamples(this SampleCountFlags sampleCountFlags) =>
		sampleCountFlags.HasFlag(SampleCountFlags.SampleCount64Bit) ? SampleCountFlags.SampleCount64Bit
		: sampleCountFlags.HasFlag(SampleCountFlags.SampleCount32Bit) ? SampleCountFlags.SampleCount32Bit
		: sampleCountFlags.HasFlag(SampleCountFlags.SampleCount16Bit) ? SampleCountFlags.SampleCount16Bit
		: sampleCountFlags.HasFlag(SampleCountFlags.SampleCount8Bit) ? SampleCountFlags.SampleCount8Bit
		: sampleCountFlags.HasFlag(SampleCountFlags.SampleCount4Bit) ? SampleCountFlags.SampleCount4Bit
		: sampleCountFlags.HasFlag(SampleCountFlags.SampleCount2Bit) ? SampleCountFlags.SampleCount2Bit
		: sampleCountFlags.HasFlag(SampleCountFlags.SampleCount1Bit) ? SampleCountFlags.SampleCount1Bit
		: 0;
	public static unsafe Result CreateShaderModule(this Vk vk, Device device, byte* bytes, nuint length, out ShaderModule shaderModule)
	{
		ShaderModuleCreateInfo shaderModuleCI = new(codeSize: length, pCode: (uint*)bytes);
		fixed (ShaderModule* shaderModulePtr = &shaderModule)
			return vk.CreateShaderModule(device, &shaderModuleCI, null, shaderModulePtr);
	}
}