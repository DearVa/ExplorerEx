using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using HandyControl.Data;
using HandyControl.Tools.Interop;

namespace HandyControl.Tools; 

public static class MouseHook {
	public static event EventHandler<MouseHookEventArgs> StatusChanged;

	private static IntPtr hookId = IntPtr.Zero;

	private static readonly InteropValues.HookProc Proc = HookCallback;

	private static int count;

	public static void Start() {
		if (hookId == IntPtr.Zero) {
			hookId = SetHook(Proc);
		}

		if (hookId != IntPtr.Zero) {
			count++;
		}
	}

	public static void Stop() {
		count--;
		if (count < 1) {
			InteropMethods.UnhookWindowsHookEx(hookId);
			hookId = IntPtr.Zero;
		}
	}

	private static IntPtr SetHook(InteropValues.HookProc proc) {
		using var curProcess = Process.GetCurrentProcess();
		using var curModule = curProcess.MainModule;

		if (curModule != null) {
			return InteropMethods.SetWindowsHookEx((int)InteropValues.HookType.WH_MOUSE_LL, proc,
				InteropMethods.GetModuleHandle(curModule.ModuleName), 0);
		}
		return IntPtr.Zero;
	}

	private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
		if (nCode < 0) {
			return InteropMethods.CallNextHookEx(hookId, nCode, wParam, lParam);
		}
		var hookStruct = Marshal.PtrToStructure<InteropValues.MOUSEHOOKSTRUCT>(lParam);
		StatusChanged?.Invoke(null, new MouseHookEventArgs {
			MessageType = (MouseHookMessageType)wParam,
			Point = new InteropValues.POINT(hookStruct.pt.X, hookStruct.pt.Y)
		});

		return InteropMethods.CallNextHookEx(hookId, nCode, wParam, lParam);
	}
}