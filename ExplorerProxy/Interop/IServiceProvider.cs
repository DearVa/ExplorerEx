using System;
using System.Runtime.InteropServices;

namespace ExplorerProxy.Interop {
	[ComImport]
	[Guid("6d5140c1-7436-11ce-8034-00aa006009fa")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IServiceProvider {
		int QueryService(ref Guid guidService, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppvObject);
	}
}
