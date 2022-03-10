using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Text;
using System.Threading;
using static HandyControl.Tools.Interop.InteropValues;

namespace HandyControl.Tools.Interop; 

internal static class InteropMethods {
	#region common

	internal const int EFail = unchecked((int)0x80004005);

	internal static readonly IntPtr HrgnNone = new(-1);

	[DllImport(ExternDll.User32, CharSet = CharSet.Auto)]
	[ResourceExposure(ResourceScope.None)]
	internal static extern int RegisterWindowMessage(string msg);

	[DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
	internal static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, out TBBUTTON lpBuffer,
		int dwSize, out int lpNumberOfBytesRead);

	[DllImport(ExternDll.Kernel32, SetLastError = true)]
	internal static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, out RECT lpBuffer,
		int dwSize, out int lpNumberOfBytesRead);

	[DllImport(ExternDll.Kernel32, SetLastError = true)]
	internal static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, out TRAYDATA lpBuffer,
		int dwSize, out int lpNumberOfBytesRead);

	[DllImport(ExternDll.User32, CharSet = CharSet.Auto)]
	internal static extern uint SendMessage(IntPtr hWnd, uint Msg, uint wParam, IntPtr lParam);

	[DllImport(ExternDll.User32, SetLastError = true, CharSet = CharSet.Auto)]
	internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

	[DllImport(ExternDll.User32, SetLastError = true, CharSet = CharSet.Auto)]
	internal static extern bool AttachThreadInput(in uint currentForegroundWindowThreadId,
		in uint thisWindowThreadId, bool isAttach);

	[DllImport(ExternDll.User32, SetLastError = true, CharSet = CharSet.Auto)]
	internal static extern IntPtr GetForegroundWindow();

	[DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
	internal static extern IntPtr OpenProcess(ProcessAccess dwDesiredAccess,
		[MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

	[DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
	internal static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, int dwSize,
		AllocationType flAllocationType, MemoryProtection flProtect);

	[DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
	internal static extern int CloseHandle(IntPtr hObject);

	[DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
	internal static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, int dwSize, FreeType dwFreeType);

	[DllImport(ExternDll.User32, SetLastError = true, CharSet = CharSet.Auto)]
	internal static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

	[DllImport(ExternDll.User32, SetLastError = true, CharSet = CharSet.Auto)]
	internal static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

	[DllImport(ExternDll.User32)]
	internal static extern int GetWindowRect(IntPtr hwnd, out RECT lpRect);

	[DllImport(ExternDll.User32, CharSet = CharSet.Auto)]
	internal static extern bool GetCursorPos(out POINT pt);

	[DllImport(ExternDll.User32)]
	internal static extern IntPtr GetDesktopWindow();

	[DllImport(ExternDll.User32, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool AddClipboardFormatListener(IntPtr hwnd);

	[DllImport(ExternDll.User32, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

	[DllImport(ExternDll.User32)]
	internal static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

	[DllImport(ExternDll.User32)]
	internal static extern bool EnableMenuItem(IntPtr hMenu, int UIDEnabledItem, int uEnable);

	[DllImport(ExternDll.User32)]
	internal static extern bool InsertMenu(IntPtr hMenu, int wPosition, int wFlags, int wIDNewItem, string lpNewItem);

	[DllImport(ExternDll.User32, ExactSpelling = true, EntryPoint = "DestroyMenu", CharSet = CharSet.Auto)]
	[ResourceExposure(ResourceScope.None)]
	internal static extern bool IntDestroyMenu(HandleRef hMenu);

	[SecurityCritical]
	[SuppressUnmanagedCodeSecurity]
	[DllImport(ExternDll.User32, SetLastError = true, ExactSpelling = true, EntryPoint = nameof(GetDC),
		CharSet = CharSet.Auto)]
	internal static extern IntPtr IntGetDC(HandleRef hWnd);

	[SecurityCritical]
	internal static IntPtr GetDC(HandleRef hWnd) {
		var hDc = IntGetDC(hWnd);
		if (hDc == IntPtr.Zero) throw new Win32Exception();

		return HandleCollector.Add(hDc, CommonHandles.HDC);
	}

	[SecurityCritical]
	[SuppressUnmanagedCodeSecurity]
	[DllImport(ExternDll.User32, ExactSpelling = true, EntryPoint = nameof(ReleaseDC), CharSet = CharSet.Auto)]
	internal static extern int IntReleaseDC(HandleRef hWnd, HandleRef hDC);

	[SecurityCritical]
	internal static int ReleaseDC(HandleRef hWnd, HandleRef hDC) {
		HandleCollector.Remove((IntPtr)hDC, CommonHandles.HDC);
		return IntReleaseDC(hWnd, hDC);
	}

	[SecurityCritical]
	[SuppressUnmanagedCodeSecurity]
	[DllImport(ExternDll.Gdi32, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Auto)]
	internal static extern int GetDeviceCaps(HandleRef hDC, int nIndex);

	[SecurityCritical]
	[SuppressUnmanagedCodeSecurity]
	[DllImport(ExternDll.User32)]
	internal static extern int GetSystemMetrics(SM nIndex);

	[SecurityCritical]
	[SuppressUnmanagedCodeSecurity]
	[DllImport(ExternDll.User32, EntryPoint = nameof(DestroyIcon), CharSet = CharSet.Auto, SetLastError = true)]
	private static extern bool IntDestroyIcon(IntPtr hIcon);

	[SecurityCritical]
	internal static bool DestroyIcon(IntPtr hIcon) {
		var result = IntDestroyIcon(hIcon);
		return result;
	}

	[SecurityCritical]
	[SuppressUnmanagedCodeSecurity]
	[DllImport(ExternDll.Gdi32, EntryPoint = nameof(DeleteObject), CharSet = CharSet.Auto, SetLastError = true)]
	private static extern bool IntDeleteObject(IntPtr hObject);

	[SecurityCritical]
	internal static bool DeleteObject(IntPtr hObject) {
		var result = IntDeleteObject(hObject);
		return result;
	}

	[SecurityCritical]
	internal static BitmapHandle CreateDIBSection(HandleRef hdc, ref BITMAPINFO bitmapInfo, int iUsage,
		ref IntPtr ppvBits, SafeFileMappingHandle hSection, int dwOffset) {
		hSection ??= new SafeFileMappingHandle(IntPtr.Zero);

		var hBitmap = PrivateCreateDIBSection(hdc, ref bitmapInfo, iUsage, ref ppvBits, hSection, dwOffset);
		return hBitmap;
	}

	[DllImport(ExternDll.Kernel32, EntryPoint = "CloseHandle", CharSet = CharSet.Auto, SetLastError = true)]
	internal static extern bool IntCloseHandle(HandleRef handle);

	[SecurityCritical]
	[SuppressUnmanagedCodeSecurity]
	[DllImport(ExternDll.Gdi32, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Auto,
		EntryPoint = nameof(CreateDIBSection))]
	private static extern BitmapHandle PrivateCreateDIBSection(HandleRef hdc, ref BITMAPINFO bitmapInfo, int iUsage,
		ref IntPtr ppvBits, SafeFileMappingHandle hSection, int dwOffset);

	[SecurityCritical]
	[SuppressUnmanagedCodeSecurity]
	[DllImport(ExternDll.User32, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Auto,
		EntryPoint = nameof(CreateIconIndirect))]
	private static extern IconHandle PrivateCreateIconIndirect([In] [MarshalAs(UnmanagedType.LPStruct)]
		ICONINFO iconInfo);

	[SecurityCritical]
	internal static IconHandle CreateIconIndirect([In] [MarshalAs(UnmanagedType.LPStruct)]
		ICONINFO iconInfo) {
		var hIcon = PrivateCreateIconIndirect(iconInfo);
		return hIcon;
	}

	[SecurityCritical]
	[SuppressUnmanagedCodeSecurity]
	[DllImport(ExternDll.Gdi32, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Auto,
		EntryPoint = nameof(CreateBitmap))]
	private static extern BitmapHandle PrivateCreateBitmap(int width, int height, int planes, int bitsPerPixel,
		byte[] lpvBits);

	[SecurityCritical]
	internal static BitmapHandle CreateBitmap(int width, int height, int planes, int bitsPerPixel, byte[] lpvBits) {
		var hBitmap = PrivateCreateBitmap(width, height, planes, bitsPerPixel, lpvBits);
		return hBitmap;
	}

	[SecurityCritical]
	[SuppressUnmanagedCodeSecurity]
	[DllImport(ExternDll.Kernel32, EntryPoint = "GetModuleFileName", CharSet = CharSet.Unicode,
		SetLastError = true)]
	private static extern int IntGetModuleFileName(HandleRef hModule, StringBuilder buffer, int length);

	[SecurityCritical]
	internal static string GetModuleFileName(HandleRef hModule) {
		var buffer = new StringBuilder(Win32Constant.MAX_PATH);
		while (true) {
			var size = IntGetModuleFileName(hModule, buffer, buffer.Capacity);
			if (size == 0) {
				throw new Win32Exception();
			}

			if (size == buffer.Capacity) {
				buffer.EnsureCapacity(buffer.Capacity * 2);
				continue;
			}

			return buffer.ToString();
		}
	}

	[SecurityCritical]
	[SuppressUnmanagedCodeSecurity]
	[DllImport(ExternDll.Shell32, CharSet = CharSet.Auto, BestFitMapping = false, ThrowOnUnmappableChar = true)]
	internal static extern int ExtractIconEx(string szExeFileName, int nIconIndex, out IconHandle phiconLarge,
		out IconHandle phiconSmall, int nIcons);

	[DllImport(ExternDll.Shell32, CharSet = CharSet.Auto)]
	internal static extern int Shell_NotifyIcon(int message, NOTIFYICONDATA pnid);

	[SecurityCritical]
	[DllImport(ExternDll.User32, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW")]
	internal static extern IntPtr CreateWindowEx(
		int dwExStyle,
		[MarshalAs(UnmanagedType.LPWStr)] string lpClassName,
		[MarshalAs(UnmanagedType.LPWStr)] string lpWindowName,
		int dwStyle,
		int x,
		int y,
		int nWidth,
		int nHeight,
		IntPtr hWndParent,
		IntPtr hMenu,
		IntPtr hInstance,
		IntPtr lpParam);

	[SecurityCritical]
	[SuppressUnmanagedCodeSecurity]
	[DllImport(ExternDll.User32, CharSet = CharSet.Unicode, SetLastError = true, BestFitMapping = false)]
	internal static extern short RegisterClass(WNDCLASS4ICON wc);

	[DllImport(ExternDll.User32, CharSet = CharSet.Auto)]
	internal static extern IntPtr DefWindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

	[DllImport(ExternDll.User32, ExactSpelling = true, CharSet = CharSet.Auto)]
	internal static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport(ExternDll.User32, CharSet = CharSet.Auto, SetLastError = true)]
	internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

	[DllImport(ExternDll.Kernel32, CharSet = CharSet.Auto, SetLastError = true)]
	internal static extern IntPtr GetModuleHandle(string lpModuleName);

	[DllImport(ExternDll.User32, CharSet = CharSet.Auto, SetLastError = true)]
	internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

	[DllImport(ExternDll.User32, CharSet = CharSet.Auto, SetLastError = true)]
	internal static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

	[DllImport(ExternDll.User32, SetLastError = true)]
	internal static extern IntPtr GetWindowDC(IntPtr window);

	[DllImport(ExternDll.Gdi32, SetLastError = true)]
	internal static extern uint GetPixel(IntPtr dc, int x, int y);

	[DllImport(ExternDll.User32, SetLastError = true)]
	internal static extern int ReleaseDC(IntPtr window, IntPtr dc);

	[DllImport(ExternDll.Gdi32, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Auto)]
	internal static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

	[DllImport(ExternDll.User32, CharSet = CharSet.Auto)]
	internal static extern IntPtr GetDC(IntPtr ptr);

	[DllImport(ExternDll.User32, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetWindowPlacement(IntPtr hwnd, WINDOWPLACEMENT lpwndpl);

	internal static WINDOWPLACEMENT GetWindowPlacement(IntPtr hwnd) {
		var wIndowplacement = new WINDOWPLACEMENT();
		if (GetWindowPlacement(hwnd, wIndowplacement)) {
			return wIndowplacement;
		}
		throw new Win32Exception(Marshal.GetLastWin32Error());
	}

	internal static int GetXLParam(int lParam) => LoWord(lParam);

	internal static int GetYLParam(int lParam) => HiWord(lParam);

	internal static int HiWord(int value) => (short)(value >> 16);

	internal static int LoWord(int value) => (short)(value & 65535);

	[DllImport(ExternDll.User32)]
	internal static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

	[DllImport(ExternDll.User32)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool EnumThreadWindows(uint dwThreadId, EnumWindowsProc lpfn, IntPtr lParam);

	[DllImport(ExternDll.Gdi32)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool DeleteDC(IntPtr hdc);

	[DllImport(ExternDll.Gdi32, SetLastError = true)]
	internal static extern IntPtr CreateCompatibleDC(IntPtr hdc);

	[DllImport(ExternDll.Gdi32, ExactSpelling = true, SetLastError = true)]
	internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

	[DllImport(ExternDll.User32, CharSet = CharSet.Unicode, SetLastError = true)]
	internal static extern IntPtr SendMessage(IntPtr hWnd, int nMsg, IntPtr wParam, IntPtr lParam);

	[DllImport(ExternDll.User32)]
	internal static extern IntPtr MonitorFromPoint(POINT pt, int flags);

	[DllImport(ExternDll.User32)]
	internal static extern IntPtr GetWindow(IntPtr hwnd, int nCmd);

	[DllImport(ExternDll.User32)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool IsWindowVisible(IntPtr hwnd);

	[DllImport(ExternDll.User32)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool IsIconic(IntPtr hwnd);

	[DllImport(ExternDll.User32)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool IsZoomed(IntPtr hwnd);

	[DllImport(ExternDll.User32, CharSet = CharSet.Auto, ExactSpelling = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, int flags);

	internal static System.Windows.Point GetCursorPos() {
		var result = default(System.Windows.Point);
		if (GetCursorPos(out var point)) {
			result.X = point.X;
			result.Y = point.Y;
		}
		return result;
	}

	[DllImport(ExternDll.User32)]
	private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

	internal static int GetWindowLong(IntPtr hWnd, GWL nIndex) => GetWindowLong(hWnd, (int)nIndex);

	internal static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong) {
		if (IntPtr.Size == 4) {
			return SetWindowLongPtr32(hWnd, nIndex, dwNewLong);
		}
		return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
	}

	[DllImport(ExternDll.User32, CharSet = CharSet.Auto, EntryPoint = "SetWindowLong")]
	internal static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

	[DllImport(ExternDll.User32, CharSet = CharSet.Auto, EntryPoint = "SetWindowLongPtr")]
	internal static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

	[DllImport(ExternDll.User32, CharSet = CharSet.Unicode)]
	private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

	[DllImport(ExternDll.User32, CharSet = CharSet.Unicode)]
	private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

	internal static IntPtr SetWindowLongPtr(IntPtr hWnd, GWLP nIndex, IntPtr dwNewLong) {
		if (IntPtr.Size == 8) {
			return SetWindowLongPtr(hWnd, (int)nIndex, dwNewLong);
		}
		return new IntPtr(SetWindowLong(hWnd, (int)nIndex, dwNewLong.ToInt32()));
	}

	internal static int SetWindowLong(IntPtr hWnd, GWL nIndex, int dwNewLong) => SetWindowLong(hWnd, (int)nIndex, dwNewLong);

	[DllImport(ExternDll.User32, CharSet = CharSet.Unicode)]
	internal static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

	[DllImport(ExternDll.Kernel32)]
	internal static extern uint GetCurrentThreadId();

	[DllImport(ExternDll.User32, CharSet = CharSet.Unicode, SetLastError = true)]
	internal static extern IntPtr CreateWindowEx(int dwExStyle, IntPtr classAtom, string lpWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

	[DllImport(ExternDll.User32)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool DestroyWindow(IntPtr hwnd);

	[DllImport(ExternDll.User32)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool UnregisterClass(IntPtr classAtom, IntPtr hInstance);

	[DllImport(ExternDll.User32)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDest, ref POINT pptDest, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, uint crKey, [In] ref BLENDFUNCTION pblend, uint dwFlags);

	[DllImport(ExternDll.User32)]
	internal static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, RedrawWindowFlags flags);

	[DllImport(ExternDll.User32)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumMonitorsDelegate lpfnEnum, IntPtr dwData);

	[DllImport(ExternDll.User32)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool IntersectRect(out RECT lprcDst, [In] ref RECT lprcSrc1, [In] ref RECT lprcSrc2);

	[DllImport(ExternDll.User32)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO monitorInfo);

	[DllImport(ExternDll.Gdi32, SetLastError = true)]
	internal static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint iUsage, out IntPtr ppvBits, IntPtr hSection, uint dwOffset);

	[DllImport(ExternDll.MsImg)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool AlphaBlend(IntPtr hdcDest, int xOriginDest, int yOriginDest, int wDest, int hDest, IntPtr hdcSrc, int xOriginSrc, int yOriginSrc, int wSrc, int hSrc, BLENDFUNCTION pfn);

	internal static int GetScParam(IntPtr wParam) => (int)wParam & 65520;

	[DllImport(ExternDll.User32)]
	internal static extern IntPtr ChildWindowFromPointEx(IntPtr hwndParent, POINT pt, int uFlags);

	[DllImport(ExternDll.Gdi32)]
	internal static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int width, int height);

	[DllImport(ExternDll.Gdi32)]
	internal static extern bool BitBlt(IntPtr hDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, int dwRop);

	[DllImport(ExternDll.User32)]
	[ResourceExposure(ResourceScope.None)]
	internal static extern bool EnableWindow(IntPtr hWnd, bool enable);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static object PtrToStructure(IntPtr lParam, Type cls) => Marshal.PtrToStructure(lParam, cls);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void PtrToStructure(IntPtr lParam, object data) => Marshal.PtrToStructure(lParam, data);

	[DllImport(ExternDll.Shell32, CallingConvention = CallingConvention.StdCall)]
	internal static extern uint SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

	[SecurityCritical]
	[DllImport(ExternDll.DwmApi, PreserveSig = true)]
	internal static extern int DwmGetColorizationColor(out uint pcrColorization, out bool pfOpaqueBlend);

	#endregion

	#region 亚克力/云母效果
	public enum AccentState {
		Disabled = 0,
		EnableGradient = 1,
		EnableTransparentGradient = 2,
		EnableBlurBehind = 3,
		EnableAcrylicBlurBehind = 4,
		InvalidState = 5
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct AccentPolicy {
		public AccentState AccentState;
		public uint AccentFlags;
		public uint GradientColor;
		public uint AnimationId;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Margins {
		public int Left;
		public int Right;
		public int Top;
		public int Bottom;
	}

	public enum WindowCompositionAttribute {
		AccentPolicy = 19
	}

	[DllImport(ExternDll.User32)]
	public static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

	public enum DwmWindowAttribute {
		TransitionsForceDisabled = 2,
		UseImmersiveDarkMode = 20,
		WindowCornerPreference = 33,
		MicaEffect = 1029
	}

	public enum DwmWindowCornerPreference {
		Default = 0,
		DoNotRound = 1,
		Round = 2,
		RoundSmall = 3
	}

	[DllImport(ExternDll.DwmApi, CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern int DwmSetWindowAttribute(IntPtr hwnd, DwmWindowAttribute attribute, ref int pvAttribute, uint cbAttribute);

	[DllImport(ExternDll.DwmApi, CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern int DwmSetWindowAttribute(IntPtr hwnd, DwmWindowAttribute attribute, ref DwmWindowCornerPreference pvAttribute, uint cbAttribute);

	[DllImport(ExternDll.DwmApi, CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

	[StructLayout(LayoutKind.Sequential)]
	public struct WindowCompositionAttributeData {
		public WindowCompositionAttribute Attribute;
		public IntPtr Data;
		public int SizeOfData;
	}

	/// <summary>
	/// 开启窗口圆角
	/// </summary>
	/// <param name="hwnd"></param>
	public static void EnableRoundCorner(IntPtr hwnd) {
		var preference = DwmWindowCornerPreference.Round;
		DwmSetWindowAttribute(hwnd, DwmWindowAttribute.WindowCornerPreference, ref preference, 4);
	}

	/// <summary>
	/// 开启窗口的亚克力透明效果
	/// </summary>
	/// <param name="hwnd"></param>
	public static void EnableAcrylic(IntPtr hwnd) {
		var accent = new AccentPolicy {
			AccentState = AccentState.EnableAcrylicBlurBehind,
			// 80: 透明度 第一个0xFFFFFF：背景色
			GradientColor = (80 << 24) | 0xFFFFFF
		};

		var sizeOfAccent = Marshal.SizeOf<AccentPolicy>();
		var pAccent = Marshal.AllocHGlobal(sizeOfAccent);
		Marshal.StructureToPtr(accent, pAccent, true);

		var data = new WindowCompositionAttributeData {
			Attribute = WindowCompositionAttribute.AccentPolicy,
			SizeOfData = sizeOfAccent,
			Data = pAccent
		};
		SetWindowCompositionAttribute(hwnd, ref data);

		Marshal.FreeHGlobal(pAccent);
	}

	/// <summary>
	/// 开启窗口投影
	/// </summary>
	/// <param name="hwnd"></param>
	public static void EnableShadows(IntPtr hwnd) {
		var v = 2;
		if (DwmSetWindowAttribute(hwnd, DwmWindowAttribute.TransitionsForceDisabled, ref v, 4) == 0) {
			var margins = new Margins();
			DwmExtendFrameIntoClientArea(hwnd, ref margins);
		}
	}
	#endregion


	internal static class Gdip {
		private const string ThreadDataSlotName = "system.drawing.threaddata";

		private static IntPtr initToken;

		private static bool Initialized => initToken != IntPtr.Zero;

		internal const int
			Ok = 0,
			GenericError = 1,
			InvalidParameter = 2,
			OutOfMemory = 3,
			ObjectBusy = 4,
			InsufficientBuffer = 5,
			NotImplemented = 6,
			Win32Error = 7,
			WrongState = 8,
			Aborted = 9,
			FileNotFound = 10,
			ValueOverflow = 11,
			AccessDenied = 12,
			UnknownImageFormat = 13,
			FontFamilyNotFound = 14,
			FontStyleNotFound = 15,
			NotTrueTypeFont = 16,
			UnsupportedGdiplusVersion = 17,
			GdiplusNotInitialized = 18,
			PropertyNotFound = 19,
			PropertyNotSupported = 20,
			EUnexpected = unchecked((int)0x8000FFFF);

		static Gdip() {
			Initialize();
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct StartupInput {
			private int GdiplusVersion;

			private readonly IntPtr DebugEventCallback;

			private bool SuppressBackgroundThread;

			private bool SuppressExternalCodecs;

			public static StartupInput GetDefault() {
				var result = new StartupInput {
					GdiplusVersion = 1,
					SuppressBackgroundThread = false,
					SuppressExternalCodecs = false
				};
				return result;
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		private readonly struct StartupOutput {
			private readonly IntPtr hook;

			private readonly IntPtr unhook;
		}

		[ResourceExposure(ResourceScope.None)]
		[ResourceConsumption(ResourceScope.AppDomain, ResourceScope.AppDomain)]
		private static void Initialize() {
			var input = StartupInput.GetDefault();

			var status = GdiplusStartup(out initToken, ref input, out _);

			if (status != Ok) {
				throw StatusException(status);
			}

			var currentDomain = AppDomain.CurrentDomain;
			currentDomain.ProcessExit += OnProcessExit;

			if (!currentDomain.IsDefaultAppDomain()) {
				currentDomain.DomainUnload += OnProcessExit;
			}
		}

		[ResourceExposure(ResourceScope.AppDomain)]
		[ResourceConsumption(ResourceScope.AppDomain)]
		private static void OnProcessExit(object sender, EventArgs e) => Shutdown();
			
		[ResourceExposure(ResourceScope.AppDomain)]
		[ResourceConsumption(ResourceScope.AppDomain)]
		private static void Shutdown() {
			if (Initialized) {
				ClearThreadData();
				// unhook our shutdown handlers as we do not need to shut down more than once
				var currentDomain = AppDomain.CurrentDomain;
				currentDomain.ProcessExit -= OnProcessExit;
				if (!currentDomain.IsDefaultAppDomain()) {
					currentDomain.DomainUnload -= OnProcessExit;
				}
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void ClearThreadData() {
			var slot = Thread.GetNamedDataSlot(ThreadDataSlotName);
			Thread.SetData(slot, null);
		}

		[DllImport(ExternDll.GdiPlus, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
		[ResourceExposure(ResourceScope.None)]
		internal static extern int GdipImageGetFrameDimensionsCount(HandleRef image, out int count);

		internal static Exception StatusException(int status) {
			return status switch {
				GenericError => new ExternalException("GdiplusGenericError"),
				InvalidParameter => new ArgumentException("GdiplusInvalidParameter"),
				OutOfMemory => new OutOfMemoryException("GdiplusOutOfMemory"),
				ObjectBusy => new InvalidOperationException("GdiplusObjectBusy"),
				InsufficientBuffer => new OutOfMemoryException("GdiplusInsufficientBuffer"),
				NotImplemented => new NotImplementedException("GdiplusNotImplemented"),
				Win32Error => new ExternalException("GdiplusGenericError"),
				WrongState => new InvalidOperationException("GdiplusWrongState"),
				Aborted => new ExternalException("GdiplusAborted"),
				FileNotFound => new FileNotFoundException("GdiplusFileNotFound"),
				ValueOverflow => new OverflowException("GdiplusOverflow"),
				AccessDenied => new ExternalException("GdiplusAccessDenied"),
				UnknownImageFormat => new ArgumentException("GdiplusUnknownImageFormat"),
				PropertyNotFound => new ArgumentException("GdiplusPropertyNotFoundError"),
				PropertyNotSupported => new ArgumentException("GdiplusPropertyNotSupportedError"),
				FontFamilyNotFound => new ArgumentException("GdiplusFontFamilyNotFound"),
				FontStyleNotFound => new ArgumentException("GdiplusFontStyleNotFound"),
				NotTrueTypeFont => new ArgumentException("GdiplusNotTrueTypeFont_NoName"),
				UnsupportedGdiplusVersion => new ExternalException("GdiplusUnsupportedGdiplusVersion"),
				GdiplusNotInitialized => new ExternalException("GdiplusNotInitialized"),
				_ => new ExternalException("GdiplusUnknown")
			};
		}

		[DllImport(ExternDll.GdiPlus, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
		[ResourceExposure(ResourceScope.None)]
		internal static extern int GdipImageGetFrameDimensionsList(HandleRef image, IntPtr buffer, int count);

		[DllImport(ExternDll.GdiPlus, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
		[ResourceExposure(ResourceScope.None)]
		internal static extern int GdipImageGetFrameCount(HandleRef image, ref Guid dimensionId, int[] count);

		[DllImport(ExternDll.GdiPlus, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
		[ResourceExposure(ResourceScope.None)]
		internal static extern int GdipGetPropertyItemSize(HandleRef image, int propid, out int size);

		[DllImport(ExternDll.GdiPlus, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
		[ResourceExposure(ResourceScope.None)]
		internal static extern int GdipGetPropertyItem(HandleRef image, int propid, int size, IntPtr buffer);

		[DllImport(ExternDll.GdiPlus, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
		[ResourceExposure(ResourceScope.Machine)]
		internal static extern int GdipCreateHBITMAPFromBitmap(HandleRef nativeBitmap, out IntPtr hbitmap, int argbBackground);

		[DllImport(ExternDll.GdiPlus, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
		[ResourceExposure(ResourceScope.None)]
		internal static extern int GdipImageSelectActiveFrame(HandleRef image, ref Guid dimensionId, int frameIndex);

		[DllImport(ExternDll.GdiPlus, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
		[ResourceExposure(ResourceScope.Machine)]
		internal static extern int GdipCreateBitmapFromFile(string filename, out IntPtr bitmap);

		[DllImport(ExternDll.GdiPlus, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
		[ResourceExposure(ResourceScope.None)]
		internal static extern int GdipImageForceValidation(HandleRef image);

		[DllImport(ExternDll.GdiPlus, SetLastError = true, ExactSpelling = true, EntryPoint = "GdipDisposeImage", CharSet = CharSet.Unicode)]
		[ResourceExposure(ResourceScope.None)]
		private static extern int IntGdipDisposeImage(HandleRef image);

		internal static int GdipDisposeImage(HandleRef image) {
			if (!Initialized) {
				return Ok;
			}
			var result = IntGdipDisposeImage(image);
			return result;
		}

		[DllImport(ExternDll.GdiPlus, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
		[ResourceExposure(ResourceScope.Process)]
		private static extern int GdiplusStartup(out IntPtr token, ref StartupInput input, out StartupOutput output);

		[DllImport(ExternDll.GdiPlus, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
		[ResourceExposure(ResourceScope.None)]
		internal static extern int GdipGetImageRawFormat(HandleRef image, ref Guid format);

		[DllImport(ExternDll.User32)]
		internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WINCOMPATTRDATA data);

		[DllImport(ExternDll.GdiPlus, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
		[ResourceExposure(ResourceScope.Machine)]
		internal static extern int GdipCreateBitmapFromStream(IStream stream, out IntPtr bitmap);

		[DllImport(ExternDll.GdiPlus, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
		[ResourceExposure(ResourceScope.Machine)]
		internal static extern int GdipCreateBitmapFromHBITMAP(HandleRef hbitmap, HandleRef hpalette, out IntPtr bitmap);

		[DllImport(ExternDll.GdiPlus, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
		[ResourceExposure(ResourceScope.None)]
		internal static extern int GdipGetImageEncodersSize(out int numEncoders, out int size);

		[DllImport(ExternDll.GdiPlus, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
		[ResourceExposure(ResourceScope.None)]
		internal static extern int GdipGetImageDecodersSize(out int numDecoders, out int size);

		[DllImport(ExternDll.GdiPlus, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
		[ResourceExposure(ResourceScope.None)]
		internal static extern int GdipGetImageDecoders(int numDecoders, int size, IntPtr decoders);

		[DllImport(ExternDll.GdiPlus, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
		[ResourceExposure(ResourceScope.None)]
		internal static extern int GdipGetImageEncoders(int numEncoders, int size, IntPtr encoders);

		[DllImport(ExternDll.GdiPlus, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
		[ResourceExposure(ResourceScope.None)]
		internal static extern int GdipSaveImageToStream(HandleRef image, IStream stream, ref Guid classId, HandleRef encoderParams);

		[DllImport(ExternDll.NTdll)]
		internal static extern int RtlGetVersion(out RTL_OSVERSIONINFOEX lpVersionInformation);
	}
}