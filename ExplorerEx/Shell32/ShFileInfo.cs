using System;
using System.Runtime.InteropServices;

namespace ExplorerEx.Shell32; 

// This structure will contain information about the file
public struct ShFileInfo {
	/// <summary>
	/// Handle to the icon representing the file
	/// </summary>
	public IntPtr hIcon;

	/// <summary>
	/// Index of the icon within the image list
	/// </summary>
	public int iIcon;

	/// <summary>
	/// Various attributes of the file
	/// </summary>
	public uint dwAttributes;

	/// <summary>
	/// Path to the file
	/// </summary>
	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
	public string szDisplayName;

	/// <summary>
	/// File type
	/// </summary>
	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
	public string szTypeName;
}

[Flags]
public enum SHGFI : uint {
	Icon = 0x000000100,     // get icon
	DisplayName = 0x000000200,     // get display name
	TypeName = 0x000000400,     // get type name
	Attributes = 0x000000800,     // get attributes
	IconLocation = 0x000001000,     // get icon location
	ExeType = 0x000002000,     // return exe type
	SysIconIndex = 0x000004000,     // get system icon index
	LinkOverlay = 0x000008000,     // put a link overlay on icon
	Selected = 0x000010000,     // show icon in selected state
	AttrSpecified = 0x000020000,     // get only specified attributes
	LargeIcon = 0x000000000,     // get large icon
	SmallIcon = 0x000000001,     // get small icon
	OpenIcon = 0x000000002,     // get open icon
	ShellIconSize = 0x000000004,     // get shell size icon
	Pidl = 0x000000008,     // pszPath is a pidl
	UseFileAttributes = 0x000000010     // use passed dwFileAttribute
}