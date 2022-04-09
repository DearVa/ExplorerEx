using System;
using System.Runtime.InteropServices;

namespace ExplorerEx.Shell32;

[ComImport, Guid("0000010c-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPersist {
	[PreserveSig]
	void GetClassID(out Guid pClassID);
}

[ComImport, Guid(Shell32Interop.IID_IPersistFile), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPersistFile : IPersist {
    new void GetClassID(out Guid pClassID);
    [PreserveSig]
    int IsDirty();

    [PreserveSig]
    int Load([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);

    [PreserveSig]
    int Save([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [In, MarshalAs(UnmanagedType.Bool)] bool fRemember);

    [PreserveSig]
    void SaveCompleted([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName);

    [PreserveSig]
    void GetCurFile([In, MarshalAs(UnmanagedType.LPWStr)] string ppszFileName);
}