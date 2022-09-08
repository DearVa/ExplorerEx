using System;
using System.Runtime.InteropServices;

namespace ExplorerProxy.Interop {
	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("000214E2-0000-0000-C000-000000000046")]
	public interface IShellBrowser {
		void GetWindow(out IntPtr phwnd);
		void ContextSensitiveHelp(bool fEnterMode);

		void InsertMenusSB(IntPtr IntPtrShared, IntPtr lpMenuWidths);

		void SetMenuSB(IntPtr IntPtrShared, IntPtr holemenuRes, IntPtr IntPtrActiveObject);

		void RemoveMenusSB(IntPtr IntPtrShared);
		void SetStatusTextSB(IntPtr pszStatusText);
		void EnableModelessSB(bool fEnable);
		void TranslateAcceleratorSB(IntPtr pmsg, ushort wID);

		void BrowseObject(IntPtr pidl, uint wFlags);
		void GetViewStateStream(uint grfMode, IntPtr ppStrm);
		void GetControlWindow(uint id, out IntPtr lpIntPtr);
		void SendControlMsg(uint id, uint uMsg, uint wParam, uint lParam, IntPtr pret);
		int QueryActiveShellView(out IntPtr ppshv);
		void OnViewWindowActive(IntPtr ppshv);
		void SetToolbarItems(IntPtr lpButtons, uint nButtons, uint uFlags);
	}
}