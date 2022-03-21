using System;
using System.Runtime.InteropServices;

namespace ExplorerEx.Shell32; 

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
internal interface IShellItem {
	void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
	void GetParent(out IShellItem ppsi);
	void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
	void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
	void Compare(IShellItem psi, uint hint, out int piOrder);
}

internal enum SIGDN : uint {
	NormalDisplay = 0,
	ParentRelativeParsing = 0x80018001,
	ParentRelativeForAddressBar = 0x8001c001,
	DesktopAbsoluteParsing = 0x80028000,
	ParentRelativeEditing = 0x80031001,
	DesktopAbsoluteEditing = 0x8004c000,
	FileSysPath = 0x80058000,
	Url = 0x80068000
}