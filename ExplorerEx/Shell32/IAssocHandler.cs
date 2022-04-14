using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace ExplorerEx.Shell32; 

[ComImport, Guid("F04061AC-1659-4a3f-A954-775AA57FC083")]
public class IAssocHandler {
	public extern int GetName([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder ppsz);

	public extern int GetUIName([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder ppsz);

	public extern int GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder ppsz, [Out] out int pIndex);

	public extern int IsRecommended();

	public extern int MakeDefault([In, MarshalAs(UnmanagedType.LPWStr)] string pszDescription);

	public extern int Invoke([In] ref IDataObject pdo);

	public extern int CreateInvoker([In] ref IDataObject pdo, [Out] out IntPtr ppInvoker);
}

[ComImport, Guid("973810ae-9599-4b88-9e4d-6ee98c9552da")]
public class IEnumAssocHandlers {
	public extern int Next([In] uint celt, [Out] out IAssocHandler rgelt, [Out] out uint pceltFetched);
}