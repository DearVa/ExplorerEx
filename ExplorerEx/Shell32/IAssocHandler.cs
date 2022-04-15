using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace ExplorerEx.Shell32;

[Guid("973810ae-9599-4b88-9e4d-6ee98c9552da"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IEnumAssocHandlers {
    [PreserveSig]
    int Next(int celt, out IAssocHandler rgelt, out int pceltFetched);
}

[Guid("f04061ac-1659-4a3f-a954-775aa57fc083"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAssocHandler {
    void GetName([MarshalAs(UnmanagedType.LPWStr)] out string ppsz);
    void GetUIName([MarshalAs(UnmanagedType.LPWStr)] out string ppsz);
    void GetIconLocation([MarshalAs(UnmanagedType.LPWStr)] out string ppszPath, out int pIndex);
    [PreserveSig]
    int IsRecommended();
    void MakeDefault([MarshalAs(UnmanagedType.LPWStr)] string pszDescription);
    void Invoke(IDataObject pdo);
    void CreateInvoker(IDataObject pdo, out /*IAssocHandlerInvoker*/ object invoker);
}

public enum AssocFilter {
	None = 0,
	Recommended = 1
}