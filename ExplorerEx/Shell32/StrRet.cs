using System;
using System.Runtime.InteropServices;

namespace ExplorerEx.Shell32; 

/// <summary>
/// Contains strings returned from the IShellFolder interface methods.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 272)]
public struct StrRet {
	/// <summary>
	/// A value that specifies the desired format of the string. This can be one of the following values.
	/// </summary>
	public enum Type {
		/// <summary>
		/// The string is at the address specified by pOleStr member.
		/// </summary>
		WStr = 0x0000,

		/// <summary>
		/// The uOffset member value indicates the number of bytes from the beginning of the item identifier list where the string is located.
		/// </summary>
		Offset = 0x0001,

		/// <summary>
		/// The string is returned in the cStr member.
		/// </summary>
		CStr = 0x0002
	}

	/// <summary>
	/// A value that specifies the desired format of the string.
	/// </summary>
	public Type uType;

	/// <summary>
	/// The string data.
	/// </summary>
	public IntPtr data;

	public override string ToString() {
		if (data == IntPtr.Zero) {
			return null;
		}
		return uType switch {
			Type.WStr => Marshal.PtrToStringUni(data),
			Type.CStr => Marshal.PtrToStringAnsi(data),
			_ => throw new InvalidOperationException()
		};
	}
}