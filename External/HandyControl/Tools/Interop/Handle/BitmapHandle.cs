using System;
using System.Runtime.InteropServices;
using System.Security;

namespace HandyControl.Tools.Interop; 

internal sealed class BitmapHandle : WpfSafeHandle {
	[SecurityCritical]
	private BitmapHandle() : this(true) {
		// 请不要删除此构造函数，否则当使用自定义ico文件时会报错
	}

	[SecurityCritical]
	private BitmapHandle(bool ownsHandle) : base(ownsHandle, CommonHandles.GDI) {
	}

	[SecurityCritical]
	protected override bool ReleaseHandle() {
		return InteropMethods.DeleteObject(handle);
	}

	[SecurityCritical]
	internal HandleRef MakeHandleRef(object obj) {
		return new HandleRef(obj, handle);
	}

	[SecurityCritical]
	internal static BitmapHandle CreateFromHandle(IntPtr hBitmap, bool ownsHandle = true) {
		return new BitmapHandle(ownsHandle) {
			handle = hBitmap,
		};
	}
}