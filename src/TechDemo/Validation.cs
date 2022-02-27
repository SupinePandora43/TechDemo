using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Vulkan;

namespace TechDemo;

unsafe partial class Program
{
	private const bool EnableValidation = true;

	private static readonly List<string> extensions = new string[]{}.ToList();



	static void Throw() => throw new Exception(Environment.StackTrace);
	static void Throw(string cause) => throw new Exception(cause);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void C(Result r){
		if(r is not Result.Success) Throw();
	}
}
