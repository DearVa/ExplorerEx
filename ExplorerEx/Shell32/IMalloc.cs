using System;
using System.Runtime.InteropServices;

namespace ExplorerEx.Shell32; 

/// <summary>
/// C# definition of the IMalloc interface.
/// </summary>
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("00000002-0000-0000-C000-000000000046")]
public interface IMalloc {
	/// <summary>
	/// Allocate a block of memory
	/// </summary>
	/// <param name="cb">Size, in bytes, of the memory block to be allocated.</param>
	/// <returns>a pointer to the allocated memory block.</returns>
	[PreserveSig]
	IntPtr Alloc([In] uint cb);

	/// <summary>
	/// Changes the size of a previously allocated memory block.
	/// </summary>
	/// <param name="pv">Pointer to the memory block to be reallocated</param>
	/// <param name="cb">Size of the memory block, in bytes, to be reallocated.</param>
	/// <returns>reallocated memory block</returns>
	[PreserveSig]
	IntPtr Realloc([In] IntPtr pv, [In] uint cb);

	/// <summary>
	/// Free a previously allocated block of memory.
	/// </summary>
	/// <param name="pv">Pointer to the memory block to be freed.</param>
	[PreserveSig]
	void Free([In] IntPtr pv);

	/// <summary>
	/// This method returns the size, in bytes, of a memory block previously allocated with IMalloc::Alloc or IMalloc::Realloc.
	/// </summary>
	/// <param name="pv">Pointer to the memory block for which the size is requested</param>
	/// <returns>The size of the allocated memory block in bytes.</returns>
	[PreserveSig]
	uint GetSize([In] IntPtr pv);

	/// <summary>
	/// This method determines whether this allocator was used to allocate the specified block of memory.
	/// </summary>
	/// <param name="pv">Pointer to the memory block</param>
	/// <returns>
	/// 1 - allocated
	/// 0 - not allocated by this IMalloc Instance.
	/// -1 if DidAlloc is unable to determine whether or not it allocated the memory block.
	/// </returns>
	[PreserveSig]
	short DidAlloc([In] IntPtr pv);

	/// <summary>
	/// Minimizes the heap by releasing unused memory to the operating system.
	/// </summary>
	[PreserveSig]
	void HeapMinimize();
}