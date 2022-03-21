using System;
using System.Runtime.InteropServices;

namespace ExplorerEx.Shell32; 

/// <summary>
/// Specifies the FMTID/PID identifier that programmatically identifies a property.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct PropertyKey {
	/// <summary>
	/// A unique GUID for the property.
	/// </summary>
	public Guid fmtid;

	/// <summary>
	/// A property identifier (PID). This parameter is not used as in SHCOLUMNID. It is recommended that you set this value to PID_FIRST_USABLE. Any value greater than or equal to 2 is acceptable.
	/// </summary>
	public uint pid;
}