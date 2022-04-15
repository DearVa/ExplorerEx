using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace ExplorerEx.Shell32; 

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid(Shell32Interop.IID_IShellItem)]
internal interface IShellItem {
	int BindToHandler(IBindCtx pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
	int GetParent(out IShellItem ppsi);
	int GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
	int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
	int Compare(IShellItem psi, uint hint, out int piOrder);
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