using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ExplorerEx.Shell32;

[ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileDialog {
	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	[PreserveSig]
	uint Show([In, Optional] IntPtr hwndOwner); //IModalWindow 


	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	uint SetFileTypes([In] uint cFileTypes, [In, MarshalAs(UnmanagedType.LPArray)] IntPtr rgFilterSpec);

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	uint SetFileTypeIndex([In] uint iFileType);

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	uint GetFileTypeIndex(out uint piFileType);

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	uint Advise([In, MarshalAs(UnmanagedType.Interface)] IntPtr pfde, out uint pdwCookie);

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	uint Unadvise([In] uint dwCookie);

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	uint SetOptions([In] FOS fos);

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	uint GetOptions(out FOS fos);

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	void SetDefaultFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	uint SetFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	uint GetFolder([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	uint GetCurrentSelection([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	uint SetFileName([In, MarshalAs(UnmanagedType.LPWStr)] string pszName);

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	uint GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	uint SetTitle([In, MarshalAs(UnmanagedType.LPWStr)] string pszTitle);

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	uint SetOkButtonLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszText);

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	uint SetFileNameLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	uint GetResult([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	uint AddPlace([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, uint fdap);

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	uint SetDefaultExtension([In, MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	uint Close([MarshalAs(UnmanagedType.Error)] uint hr);

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	uint SetClientGuid([In] ref Guid guid);

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	uint ClearClientData();

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	uint SetFilter([MarshalAs(UnmanagedType.Interface)] IntPtr pFilter);
}