using System.Runtime.InteropServices;

namespace ExplorerProxy.Interop {
	[StructLayout(LayoutKind.Sequential)]
	internal struct POINT {
		public int x;
		public int y;
	}
}
