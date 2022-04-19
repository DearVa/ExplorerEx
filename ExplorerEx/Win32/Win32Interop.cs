using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ExplorerEx.Win32;

public static class Win32Interop {
#pragma warning disable CS0649
	// ReSharper disable InconsistentNaming
	// ReSharper disable IdentifierTypo
	// ReSharper disable StringLiteralTypo
	// ReSharper disable UnusedMember.Global
	// ReSharper disable FieldCanBeMadeReadOnly.Local
	// ReSharper disable FieldCanBeMadeReadOnly.Global
	// ReSharper disable UnusedType.Global
	private const string Gdi32 = "gdi32.dll";
	private const string User32 = "user32.dll";
	private const string Kernel32 = "kernel32.dll";

	private const string Ntdll = "ntdll.dll";
	private const string Dwmapi = "dwmapi.dll";


	[DllImport(User32)]
	public static extern IntPtr SetCursor(IntPtr hCursor);

	[DllImport(User32)]
	public static extern IntPtr LoadCursor(IntPtr hInstance, long lpCursorName);

	[DllImport(User32)]
	public static extern bool GetCursorPos(out Point point);

	[DllImport(User32, CharSet = CharSet.Auto, ExactSpelling = true, SetLastError = true)]
	public static extern bool GetIconInfo(IntPtr hIcon, IntPtr pIconInfo);

	[DllImport(User32, CharSet = CharSet.Auto, ExactSpelling = true, SetLastError = true)]
	public static extern bool DestroyIcon(IntPtr hIcon);

	[DllImport(User32)]
	public static extern int GetDoubleClickTime();

	[DllImport(User32)]
	public static extern IntPtr CreatePopupMenu();

	[DllImport(User32)]
	public static extern int GetMenuItemCount(IntPtr hMenu);

	[DllImport(User32)]
	public static extern int GetMenuString(IntPtr hMenu, uint uIDItem, [Out] StringBuilder lpString, int nMaxCount, uint uFlag);
	
	[DllImport(User32)]
	public static extern uint GetMenuItemID(IntPtr hMenu, uint uIDItem);

	[DllImport(User32)]
	public static extern bool DestroyMenu(IntPtr hMenu);
	
	[DllImport(User32)]
	public static extern IntPtr WindowFromPoint(Point pos);

	/// <summary>
	/// 获取光标处窗口的HWND
	/// </summary>
	/// <returns></returns>
	public static IntPtr GetCursorHwnd() {
		GetCursorPos(out var p);
		return WindowFromPoint(p);
	}

	[DllImport(Gdi32)]
	public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

	[DllImport(Gdi32)]
	public static extern bool DeleteDC(IntPtr hdc);

	[DllImport(Gdi32)]
	public static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

	[DllImport(Gdi32)]
	public static extern int GetObject(IntPtr h, int c, IntPtr pv);

	[DllImport(Gdi32)]
	public static extern int GetDIBits(ref IntPtr hdc, ref IntPtr hbm, uint start, uint cLines, IntPtr lpvBits, IntPtr lpbmi, uint usage);

	[DllImport(Gdi32)]
	public static extern bool DeleteObject(IntPtr hObject);

	[StructLayout(LayoutKind.Sequential)]
	public struct Bitmap {
		public int bmType;
		public int bmWidth;
		public int bmHeight;
		public int bmWidthBytes;
		public short bmPlanes;
		public short bmBitsPixel;
		public IntPtr bmBits;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct BitmapInfoHeader {
		public int biSize;
		public int biWidth;
		public int biHeight;
		public short biPlanes;
		public short biBitCount;
		public int biCompression;
		public int biSizeImage;
		public int biXPelsPerMeter;
		public int biYPelsPerMeter;
		public int biClrUsed;
		public int biClrImportant;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct BitmapInfo {
		public BitmapInfoHeader bmiHeader;
		public IntPtr bmiColors;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct IconInfo {
		public bool fIcon;
		public int xHotspot;
		public int yHotspot;
		public IntPtr hbmMask;
		public IntPtr hbmColor;
	}

	[DllImport(Kernel32)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool CloseHandle(IntPtr handle);

	[StructLayout(LayoutKind.Sequential)]
	public struct Rect {
		public int x;
		public int y;
		public int width;
		public int height;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Point {
		public int x;
		public int y;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct Size {
		public int width;
		public int height;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct RgbQuad {
		public byte b;
		public byte g;
		public byte r;
		public byte reserved;
	}

	[DllImport(User32, SetLastError = true)]
	public static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);

	[DllImport(User32, CharSet = CharSet.Auto)]
	public static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);

	[DllImport(User32, CharSet = CharSet.Auto)]
	public static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);

	public enum WinMessage {
		CopyData = 0x004A,
		DeviceChange = 0x0219,
		DrawClipboard = 0x0308,
		ChangeCbChain = 0x030D,
		/// <summary>
		/// 系统主题色改变
		/// </summary>
		DwmColorizationColorChanged = 0x0320,
		User = 0x0400
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct DevBroadcastVolume {
		public int size;
		public int deviceType;
		public int reserved;
		public int unitMask;
	}

	public static char DriveMaskToLetter(int mask) {
		char letter;
		const string drives = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"; //1 = A, 2 = B, 3 = C
		var cnt = 0;
		var pom = mask / 2;
		while (pom != 0) {  // while there is any bit set in the mask shift it right   
			pom /= 2;
			cnt++;
		}
		if (cnt < drives.Length) {
			letter = drives[cnt];
		} else {
			letter = '?';
		}
		return letter;
	}

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

	[DllImport(User32)]
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

	[DllImport(Dwmapi, CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern int DwmSetWindowAttribute(IntPtr hwnd, DwmWindowAttribute attribute, ref int pvAttribute, uint cbAttribute);

	[DllImport(Dwmapi, CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern int DwmSetWindowAttribute(IntPtr hwnd, DwmWindowAttribute attribute, ref DwmWindowCornerPreference pvAttribute, uint cbAttribute);

	[DllImport(Dwmapi, CharSet = CharSet.Unicode, SetLastError = true)]
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
			// 20: 透明度  0xFFFFFF：背景色
			GradientColor = (20 << 24) | 0xFFFFFF
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

	#region 获取进程命令行

	[Flags]
	public enum OpenProcessDesiredAccess : uint {
		VmRead = 0x0010,
		QueryInformation = 0x0400,
		QueryLimitedInformation = 0x1000
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct ProcessBasicInformation {
		public IntPtr Reserved1;
		public IntPtr PebBaseAddress;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
		public IntPtr[] Reserved2;
		public IntPtr UniqueProcessId;
		public IntPtr Reserved3;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct UnicodeString {
		public ushort Length;
		public ushort MaximumLength;
		public IntPtr Buffer;
	}

	// This is not the real struct!
	// I faked it to get ProcessParameters address.
	// Actual struct definition:
	// https://docs.microsoft.com/en-us/windows/win32/api/winternl/ns-winternl-peb
	[StructLayout(LayoutKind.Sequential)]
	public struct PEB {
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
		public IntPtr[] Reserved;
		public IntPtr ProcessParameters;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct RtlUserProcessParameters {
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public byte[] Reserved1;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
		public IntPtr[] Reserved2;
		public UnicodeString ImagePathName;
		public UnicodeString CommandLine;
	}

	[DllImport(Ntdll)]
	public static extern uint NtQueryInformationProcess(IntPtr ProcessHandle, uint ProcessInformationClass, IntPtr ProcessInformation, uint ProcessInformationLength, out uint ReturnLength);

	[DllImport(Kernel32)]
	public static extern IntPtr OpenProcess(OpenProcessDesiredAccess dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

	[DllImport(Kernel32)]
	public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, uint nSize, out uint lpNumberOfBytesRead);

	private static bool ReadStructFromProcessMemory<TStruct>(
		IntPtr hProcess, IntPtr lpBaseAddress, out TStruct val) {
		val = default;
		var structSize = Marshal.SizeOf<TStruct>();
		var mem = Marshal.AllocHGlobal(structSize);
		try {
			if (ReadProcessMemory(hProcess, lpBaseAddress, mem, (uint)structSize, out var len) && len == structSize) {
				val = Marshal.PtrToStructure<TStruct>(mem);
				return true;
			}
		} finally {
			Marshal.FreeHGlobal(mem);
		}
		return false;
	}

	public static string GetCommandLine(this Process process) {
		var hProcess = OpenProcess(
			OpenProcessDesiredAccess.QueryInformation |
			OpenProcessDesiredAccess.VmRead, false, (uint)process.Id);
		if (hProcess == IntPtr.Zero) {
			throw new ApplicationException("Couldn't open process for VM read");
		}
		try {
			var sizePBI = Marshal.SizeOf<ProcessBasicInformation>();
			var memPBI = Marshal.AllocHGlobal(sizePBI);
			try {
				var ret = NtQueryInformationProcess(hProcess, 0, memPBI, (uint)sizePBI, out _);
				if (ret != 0) {
					throw new ApplicationException("NtQueryInformationProcess failed");
				}
				var pbiInfo = Marshal.PtrToStructure<ProcessBasicInformation>(memPBI);
				if (pbiInfo.PebBaseAddress == IntPtr.Zero) {
					throw new ApplicationException("PebBaseAddress is null");
				}
				if (!ReadStructFromProcessMemory<PEB>(hProcess, pbiInfo.PebBaseAddress, out var pebInfo)) {
					throw new ApplicationException("Couldn't read PEB information");
				}
				if (!ReadStructFromProcessMemory<RtlUserProcessParameters>(hProcess, pebInfo.ProcessParameters, out var ruppInfo)) {
					throw new ApplicationException("Couldn't read ProcessParameters");
				}
				var clLen = ruppInfo.CommandLine.MaximumLength;
				var memCL = Marshal.AllocHGlobal(clLen);
				try {
					if (ReadProcessMemory(hProcess, ruppInfo.CommandLine.Buffer, memCL, clLen, out _)) {
						return Marshal.PtrToStringUni(memCL);
					} else {
						throw new Win32Exception("Couldn't read command line buffer");
					}
				} finally {
					Marshal.FreeHGlobal(memCL);
				}
			} finally {
				Marshal.FreeHGlobal(memPBI);
			}
		} finally {
			CloseHandle(hProcess);
		}
	}

	#endregion

	#region 控制台

	[DllImport(Kernel32, SetLastError = true)]
	internal static extern int AllocConsole();

	[DllImport(Kernel32, SetLastError = true)]
	internal static extern int FreeConsole();

	[DllImport(Kernel32, SetLastError = true)]
	internal static extern IntPtr GetConsoleWindow();

	[DllImport(Kernel32, SetLastError = true)]
	internal static extern bool AttachConsole(int dwProcessId);

	#endregion

	#region Appx
	[Flags]
	public enum PackageConstants {
		FilterAllLoaded = 0x00000000,
		PropertyFramework = 0x00000001,
		PropertyResource = 0x00000002,
		PropertyBundle = 0x00000004,
		FilterHead = 0x00000010,
		FilterDirect = 0x00000020,
		FilterResource = 0x00000040,
		FilterBundle = 0x00000080,
		InformationBasic = 0x00000000,
		InformationFull = 0x00000100,
		PropertyDevelopmentMode = 0x00010000,
	}

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct PackageInfo {
		public int reserved;
		public int flags;
		public IntPtr path;
		public IntPtr packageFullName;
		public IntPtr packageFamilyName;
		public PackageID packageId;
	}

	public enum AppxPackageArchitecture {
		x86 = 0,
		Arm = 5,
		x64 = 9,
		Neutral = 11,
		Arm64 = 12
	}

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct PackageID {
		public int reserved;
		public AppxPackageArchitecture processorArchitecture;
		public ushort VersionRevision;
		public ushort VersionBuild;
		public ushort VersionMinor;
		public ushort VersionMajor;
		public IntPtr name;
		public IntPtr publisher;
		public IntPtr resourceId;
		public IntPtr publisherId;
	}

	[DllImport(Kernel32, CharSet = CharSet.Unicode)]
	public static extern int GetPackageFullName(IntPtr hProcess, ref int packageFullNameLength, StringBuilder packageFullName);

	[DllImport(Kernel32, CharSet = CharSet.Unicode)]
	public static extern int OpenPackageInfoByFullName(string packageFullName, int reserved, out IntPtr pPackageInfo);

	[DllImport(Kernel32, CharSet = CharSet.Unicode)]
	public static extern int GetPackageInfo(IntPtr pPackageInfo, PackageConstants flags, ref int bufferLength, IntPtr buffer, out int count);

	[DllImport(Kernel32, CharSet = CharSet.Unicode)]
	public static extern int ClosePackageInfo(IntPtr pPackageInfo);

	#endregion
	// ReSharper restore InconsistentNaming
	// ReSharper restore IdentifierTypo
	// ReSharper restore StringLiteralTypo
	// ReSharper restore UnusedMember.Global
	// ReSharper restore FieldCanBeMadeReadOnly.Local
	// ReSharper restore FieldCanBeMadeReadOnly.Global
	// ReSharper restore UnusedType.Global
#pragma warning restore CS0649
}