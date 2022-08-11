using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace ExplorerEx.Shell32; 

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid(Shell32Interop.IID_IShellItem)]
internal interface IShellItem {
	int BindToHandler(IBindCtx? pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
	int GetParent(out IShellItem ppsi);
	int GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
	int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
	int Compare(IShellItem psi, uint hint, out int piOrder);
}

[ComImport, Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemArray {
    // Not supported: IBindCtx
    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    void BindToHandler([In] IntPtr pbc, [In] ref Guid rbhid, [In] ref Guid riid, out IntPtr ppvOut);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    void GetPropertyStore([In] int Flags, [In] ref Guid riid, out IntPtr ppv);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    void GetPropertyDescriptionList([In] IntPtr keyType, [In] ref Guid riid, out IntPtr ppv);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    void GetAttributes([In] IntPtr dwAttribFlags, [In] uint sfgaoMask, out uint psfgaoAttribs);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    void GetCount(out uint pdwNumItems);

    [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    void GetItemAt([In] uint dwIndex, [MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

    // Not supported: IEnumShellItems (will use GetCount and GetItemAt instead)
    [Obsolete, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    void EnumItems([MarshalAs(UnmanagedType.Interface)] out IntPtr ppenumShellItems);
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