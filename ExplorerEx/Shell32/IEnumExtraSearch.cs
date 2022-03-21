﻿using System;
using System.Runtime.InteropServices;

namespace ExplorerEx.Shell32; 

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("0E700BE1-9DB6-11d1-A1CE-00C04FD75D13")]
public interface IEnumExtraSearch {
	/// <summary>
	/// Used to request information on one or more search objects.
	/// </summary>
	/// <param name="celt">The number of search objects to be enumerated, starting from the current object. If celt is too large, the method should stop and return the actual number of search objects in pceltFetched.</param>
	/// <param name="rgelt">A pointer to an array of pceltFetched EXTRASEARCH structures containing information on the enumerated objects.</param>
	/// <param name="pceltFetched">The number of objects actually enumerated. This may be less than celt.</param>
	/// <returns>Returns S_OK if successful, or a COM-defined error code otherwise.</returns>
	[PreserveSig]
	int Next(uint celt, [MarshalAs(UnmanagedType.LPArray)] ExtraSearch[] rgelt, out uint pceltFetched);

	/// <summary>
	/// Skip a specified number of objects.
	/// </summary>
	/// <param name="celt">The number of objects to skip.</param>
	/// <returns>Returns S_OK if successful, or a COM-defined error code otherwise.</returns>
	[PreserveSig]
	int Skip(uint celt);

	/// <summary>
	/// Used to reset the enumeration index to zero.
	/// </summary>
	/// <returns>Returns S_OK if successful, or a COM-defined error code otherwise.</returns>
	[PreserveSig]
	int Reset();

	/// <summary>
	/// Used to request a duplicate of the enumerator object to preserve its current state.
	/// </summary>
	/// <param name="ppenum">A pointer to the IEnumExtraSearch interface of a new enumerator object.</param>
	/// <returns>Returns S_OK if successful, or a COM-defined error code otherwise.</returns>
	[PreserveSig]
	int Clone(out IEnumExtraSearch ppenum);
}

[StructLayout(LayoutKind.Sequential)]
public struct ExtraSearch {
	/// <summary>
	/// A search object's GUID.
	/// </summary>
	public Guid guidSearch;

	/// <summary>
	/// A Unicode string containing the search object's friendly name. It will be used to identify the search engine on the Search Assistant menu.
	/// </summary>
	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
	public string wszFriendlyName;

	/// <summary>
	/// The URL that will be displayed in the search pane.
	/// </summary>
	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2048)]
	public string wszUrl;
}
