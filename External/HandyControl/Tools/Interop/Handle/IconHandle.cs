using System;
using System.Security;

namespace HandyControl.Tools.Interop; 

internal sealed class IconHandle : WpfSafeHandle {
	[SecurityCritical]
	private IconHandle() : base(true, CommonHandles.Icon) {
	}

	[SecurityCritical]
	protected override bool ReleaseHandle() {
		return InteropMethods.DestroyIcon(handle);
	}

	[SecurityCritical, SecuritySafeCritical]
	internal static IconHandle GetInvalidIcon() {
		return new IconHandle();
	}

	[SecurityCritical]
	internal IntPtr CriticalGetHandle() {
		return handle;
	}
}