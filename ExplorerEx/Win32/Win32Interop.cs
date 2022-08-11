using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using static ExplorerEx.Win32.Win32Interop;
using System.Windows.Controls;

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
	public static extern bool GetCursorPos(out PointW point);

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
	public static extern IntPtr WindowFromPoint(PointW pos);

	/// <summary>
	/// 获取光标处窗口的HWND
	/// </summary>
	/// <returns></returns>
	public static IntPtr GetCursorHwnd() {
		GetCursorPos(out var p);
		return WindowFromPoint(p);
	}

	[DllImport(User32, SetLastError = true, CharSet = CharSet.Unicode)]
	public static extern int MessageBox(IntPtr hWnd, string text, string caption, MessageBoxType type);

	public enum MessageBoxType {
		Ok = 0x0,
		OkCancel = 0x1,
		AbortRetryIgnore = 0x2,
		YesNoCancel = 0x3,
		YesNo = 0x4,
		RetryCancel = 0x5,
		CancelTryContinue = 0x6,
		IconHand = 0x10,
		IconQuestion = 0x20,
		IconExclamation = 0x30,
		IconAsterisk = 0x40,
		IconWarning = IconExclamation,
		IconError = IconHand,
		IconInformation = IconAsterisk,
		IconStop = IconHand,
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
	public struct RectW {
		public int x;
		public int y;
		public int width;
		public int height;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct PointW {
		public int x;
		public int y;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct SizeW {
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
		NcHitTest = 0x0084,
		NcLButtonDown = 0x00A1,
		NcLButtonUp = 0x00A2,
		NcMouseLeave = 0x02A2,
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

	public const int GWL_STYLE = -16;
	public const int WS_SYSMENU = 0x80000;

	[DllImport(User32)]
	public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

	[DllImport(User32)]
	public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

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

	[Flags]
	public enum MenuItemInfoMask : uint {
		//
		// Summary:
		//     Retrieves or sets the fState member.
		State = 1,
		//
		// Summary:
		//     Retrieves or sets the wID member.
		ID = 2,
		//
		// Summary:
		//     Retrieves or sets the hSubMenu member.
		Submenu = 4,
		//
		// Summary:
		//     Retrieves or sets the hbmpChecked and hbmpUnchecked members.
		CheckMarks = 8,
		//
		// Summary:
		//     Retrieves or sets the fType and dwTypeData members.
		//     MIIM_TYPE is replaced by MIIM_BITMAP, MIIM_FTYPE, and MIIM_STRING.
		Type = 16,
		//
		// Summary:
		//     Retrieves or sets the dwItemData member.
		Data = 32,
		//
		// Summary:
		//     Retrieves or sets the dwTypeData member.
		String = 64,
		//
		// Summary:
		//     Retrieves or sets the hbmpItem member.
		Bitmap = 128,
		//
		// Summary:
		//     Retrieves or sets the fType member.
		FType = 256
	}

	public enum MenuItemType : uint {
		//
		// Summary:
		//     Displays the menu item using a text string. The dwTypeData member is the pointer
		//     to a null-terminated string, and the cch member is the length of the string.
		//     MFT_STRING is replaced by MIIM_STRING.
		String = 0,
		//
		// Summary:
		//     Displays the menu item using a bitmap. The low-order word of the dwTypeData member
		//     is the bitmap handle, and the cch member is ignored.
		//     MFT_BITMAP is replaced by MIIM_BITMAP and hbmpItem.
		Bitmap = 4,
		//
		// Summary:
		//     Places the menu item on a new line (for a menu bar) or in a new column (for a
		//     drop-down menu, submenu, or shortcut menu). For a drop-down menu, submenu, or
		//     shortcut menu, a vertical line separates the new column from the old.
		MenuBarBreak = 32,
		//
		// Summary:
		//     Places the menu item on a new line (for a menu bar) or in a new column (for a
		//     drop-down menu, submenu, or shortcut menu). For a drop-down menu, submenu, or
		//     shortcut menu, the columns are not separated by a vertical line.
		MenuBreak = 64,
		//
		// Summary:
		//     Assigns responsibility for drawing the menu item to the window that owns the
		//     menu. The window receives a WM_MEASUREITEM message before the menu is displayed
		//     for the first time, and a WM_DRAWITEM message whenever the appearance of the
		//     menu item must be updated. If this value is specified, the dwTypeData member
		//     contains an application-defined value.
		OwnerDraw = 256,
		//
		// Summary:
		//     Displays selected menu items using a radio-button mark instead of a check mark
		//     if the hbmpChecked member is NULL.
		RadioCheck = 512,
		//
		// Summary:
		//     Specifies that the menu item is a separator. A menu item separator appears as
		//     a horizontal dividing line. The dwTypeData and cch members are ignored. This
		//     value is valid only in a drop-down menu, submenu, or shortcut menu.
		Separator = 2048,
		//
		// Summary:
		//     Specifies that menus cascade right-to-left (the default is left-to-right). This
		//     is used to support right-to-left languages, such as Arabic and Hebrew.
		RightOrder = 8192,
		//
		// Summary:
		//     Right-justifies the menu item and any subsequent items. This value is valid only
		//     if the menu item is in a menu bar.
		RightJustify = 16384
	}

	[Flags]
	public enum MenuItemState : uint {
		//
		// Summary:
		//     Enables the menu item so that it can be selected. This is the default state.
		Enabled = 0,
		//
		// Summary:
		//     Unchecks the menu item. For more information about clear menu items, see the
		//     hbmpChecked member.
		Unchecked = 0,
		//
		// Summary:
		//     Removes the highlight from the menu item. This is the default state.
		UnHilite = 0,
		//
		// Summary:
		//     Disables the menu item and grays it so that it cannot be selected. This is equivalent
		//     to MFS_DISABLED.
		Grayed = 3,
		//
		// Summary:
		//     Disables the menu item and grays it so that it cannot be selected. This is equivalent
		//     to MFS_GRAYED.
		Disabled = 3,
		//
		// Summary:
		//     Checks the menu item. For more information about selected menu items, see the
		//     hbmpChecked member.
		Checked = 8,
		//
		// Summary:
		//     Highlights the menu item.
		Hilite = 128,
		//
		// Summary:
		//     Specifies that the menu item is the default. A menu can contain only one default
		//     menu item, which is displayed in bold.
		Default = 4096
	}

	public struct MenuItemInfo {
		//
		// Summary:
		//     Type: UINT
		//     The size of the structure, in bytes. The caller must set this member to .
		public uint cbSize;
		//
		// Summary:
		//     Type: UINT
		//     Indicates the members to be retrieved or set. This member can be one or more
		//     of the following values.
		//     Value – Meaning –
		//     MIIM_BITMAP 0x00000080 – Retrieves or sets the hbmpItem member. –
		//     MIIM_CHECKMARKS 0x00000008 – Retrieves or sets the hbmpChecked and hbmpUnchecked
		//     members. –
		//     MIIM_DATA 0x00000020 – Retrieves or sets the dwItemData member. –
		//     MIIM_FTYPE 0x00000100 – Retrieves or sets the fType member. –
		//     MIIM_ID 0x00000002 – Retrieves or sets the wID member. –
		//     MIIM_STATE 0x00000001 – Retrieves or sets the fState member. –
		//     MIIM_STRING 0x00000040 – Retrieves or sets the dwTypeData member. –
		//     MIIM_SUBMENU 0x00000004 – Retrieves or sets the hSubMenu member. –
		//     MIIM_TYPE 0x00000010 – Retrieves or sets the fType and dwTypeData members. MIIM_TYPE
		//     is replaced by MIIM_BITMAP, MIIM_FTYPE, and MIIM_STRING. –
		public MenuItemInfoMask fMask;
		//
		// Summary:
		//     Type: UINT
		//     The menu item type. This member can be one or more of the following values.
		//     The MFT_BITMAP, MFT_SEPARATOR, and MFT_STRING values cannot be combined with
		//     one another. Set fMask to MIIM_TYPE to use fType.
		//     fType is used only if fMask has a value of MIIM_FTYPE.
		//     Value – Meaning –
		//     MFT_BITMAP 0x00000004L – Displays the menu item using a bitmap. The low-order
		//     word of the dwTypeData member is the bitmap handle, and the cch member is ignored.
		//     MFT_BITMAP is replaced by MIIM_BITMAP and hbmpItem. –
		//     MFT_MENUBARBREAK 0x00000020L – Places the menu item on a new line (for a menu
		//     bar) or in a new column (for a drop-down menu, submenu, or shortcut menu). For
		//     a drop-down menu, submenu, or shortcut menu, a vertical line separates the new
		//     column from the old. –
		//     MFT_MENUBREAK 0x00000040L – Places the menu item on a new line (for a menu bar)
		//     or in a new column (for a drop-down menu, submenu, or shortcut menu). For a drop-down
		//     menu, submenu, or shortcut menu, the columns are not separated by a vertical
		//     line. –
		//     MFT_OWNERDRAW 0x00000100L – Assigns responsibility for drawing the menu item
		//     to the window that owns the menu. The window receives a WM_MEASUREITEM message
		//     before the menu is displayed for the first time, and a WM_DRAWITEM message whenever
		//     the appearance of the menu item must be updated. If this value is specified,
		//     the dwTypeData member contains an application-defined value. –
		//     MFT_RADIOCHECK 0x00000200L – Displays selected menu items using a radio-button
		//     mark instead of a check mark if the hbmpChecked member is NULL. –
		//     MFT_RIGHTJUSTIFY 0x00004000L – Right-justifies the menu item and any subsequent
		//     items. This value is valid only if the menu item is in a menu bar. –
		//     MFT_RIGHTORDER 0x00002000L – Specifies that menus cascade right-to-left (the
		//     default is left-to-right). This is used to support right-to-left languages, such
		//     as Arabic and Hebrew. –
		//     MFT_SEPARATOR 0x00000800L – Specifies that the menu item is a separator. A menu
		//     item separator appears as a horizontal dividing line. The dwTypeData and cch
		//     members are ignored. This value is valid only in a drop-down menu, submenu, or
		//     shortcut menu. –
		//     MFT_STRING 0x00000000L – Displays the menu item using a text string. The dwTypeData
		//     member is the pointer to a null-terminated string, and the cch member is the
		//     length of the string. MFT_STRING is replaced by MIIM_STRING. –
		public MenuItemType fType;
		//
		// Summary:
		//     Type: UINT
		//     The menu item state. This member can be one or more of these values. Set fMask
		//     to MIIM_STATE to use fState.
		//     Value – Meaning –
		//     MFS_CHECKED 0x00000008L – Checks the menu item. For more information about selected
		//     menu items, see the hbmpChecked member. –
		//     MFS_DEFAULT 0x00001000L – Specifies that the menu item is the default. A menu
		//     can contain only one default menu item, which is displayed in bold. –
		//     MFS_DISABLED 0x00000003L – Disables the menu item and grays it so that it cannot
		//     be selected. This is equivalent to MFS_GRAYED. –
		//     MFS_ENABLED 0x00000000L – Enables the menu item so that it can be selected. This
		//     is the default state. –
		//     MFS_GRAYED 0x00000003L – Disables the menu item and grays it so that it cannot
		//     be selected. This is equivalent to MFS_DISABLED. –
		//     MFS_HILITE 0x00000080L – Highlights the menu item. –
		//     MFS_UNCHECKED 0x00000000L – Unchecks the menu item. For more information about
		//     clear menu items, see the hbmpChecked member. –
		//     MFS_UNHILITE 0x00000000L – Removes the highlight from the menu item. This is
		//     the default state. –
		public MenuItemState fState;
		//
		// Summary:
		//     Type: UINT
		//     An application-defined value that identifies the menu item. Set fMask to MIIM_ID
		//     to use wID.
		public uint wID;
		//
		// Summary:
		//     Type: HMENU
		//     A handle to the drop-down menu or submenu associated with the menu item. If the
		//     menu item is not an item that opens a drop-down menu or submenu, this member
		//     is NULL. Set fMask to MIIM_SUBMENU to use hSubMenu.
		public IntPtr hSubMenu;
		//
		// Summary:
		//     Type: HBITMAP
		//     A handle to the bitmap to display next to the item if it is selected. If this
		//     member is NULL, a default bitmap is used. If the MFT_RADIOCHECK type value is
		//     specified, the default bitmap is a bullet. Otherwise, it is a check mark. Set
		//     fMask to MIIM_CHECKMARKS to use hbmpChecked.
		public IntPtr hbmpChecked;
		//
		// Summary:
		//     Type: HBITMAP
		//     A handle to the bitmap to display next to the item if it is not selected. If
		//     this member is NULL, no bitmap is used. Set fMask to MIIM_CHECKMARKS to use hbmpUnchecked.
		public IntPtr hbmpUnchecked;
		//
		// Summary:
		//     Type: ULONG_PTR
		//     An application-defined value associated with the menu item. Set fMask to MIIM_DATA
		//     to use dwItemData.
		public IntPtr dwItemData;
		//
		// Summary:
		//     Type: LPTSTR
		//     The contents of the menu item. The meaning of this member depends on the value
		//     of fType and is used only if the MIIM_TYPE flag is set in the fMask member.
		//     To retrieve a menu item of type MFT_STRING, first find the size of the string
		//     by setting the dwTypeData member of MENUITEMINFO to NULL and then calling GetMenuItemInfo.
		//     The value of cch+1 is the size needed. Then allocate a buffer of this size, place
		//     the pointer to the buffer in dwTypeData, increment cch, and call GetMenuItemInfo
		//     once again to fill the buffer with the string. If the retrieved menu item is
		//     of some other type, then GetMenuItemInfo sets the dwTypeData member to a value
		//     whose type is specified by the fType member.
		//     When using with the SetMenuItemInfo function, this member should contain a value
		//     whose type is specified by the fType member.
		//     dwTypeData is used only if the MIIM_STRING flag is set in the fMask member
		public IntPtr dwTypeData;
		//
		// Summary:
		//     Type: UINT
		//     The length of the menu item text, in characters, when information is received
		//     about a menu item of the MFT_STRING type. However, cch is used only if the MIIM_TYPE
		//     flag is set in the fMask member and is zero otherwise. Also, cch is ignored when
		//     the content of a menu item is set by calling SetMenuItemInfo.
		//     Note that, before calling GetMenuItemInfo, the application must set cch to the
		//     length of the buffer pointed to by the dwTypeData member. If the retrieved menu
		//     item is of type MFT_STRING (as indicated by the fType member), then GetMenuItemInfo
		//     changes cch to the length of the menu item text. If the retrieved menu item is
		//     of some other type, GetMenuItemInfo sets the cch field to zero.
		//     The cch member is used when the MIIM_STRING flag is set in the fMask member.
		public uint cch;
		//
		// Summary:
		//     Type: HBITMAP
		//     A handle to the bitmap to be displayed, or it can be one of the values in the
		//     following table. It is used when the MIIM_BITMAP flag is set in the fMask member.
		//     Value – Meaning –
		//     HBMMENU_CALLBACK ((HBITMAP) -1) – A bitmap that is drawn by the window that owns
		//     the menu. The application must process the WM_MEASUREITEM and WM_DRAWITEM messages.
		//     –
		//     HBMMENU_MBAR_CLOSE ((HBITMAP) 5) – Close button for the menu bar. –
		//     HBMMENU_MBAR_CLOSE_D ((HBITMAP) 6) – Disabled close button for the menu bar.
		//     –
		//     HBMMENU_MBAR_MINIMIZE ((HBITMAP) 3) – Minimize button for the menu bar. –
		//     HBMMENU_MBAR_MINIMIZE_D ((HBITMAP) 7) – Disabled minimize button for the menu
		//     bar. –
		//     HBMMENU_MBAR_RESTORE ((HBITMAP) 2) – Restore button for the menu bar. –
		//     HBMMENU_POPUP_CLOSE ((HBITMAP) 8) – Close button for the submenu. –
		//     HBMMENU_POPUP_MAXIMIZE ((HBITMAP) 10) – Maximize button for the submenu. –
		//     HBMMENU_POPUP_MINIMIZE ((HBITMAP) 11) – Minimize button for the submenu. –
		//     HBMMENU_POPUP_RESTORE ((HBITMAP) 9) – Restore button for the submenu. –
		//     HBMMENU_SYSTEM ((HBITMAP) 1) – Windows icon or the icon of the window specified
		//     in dwItemData. –
		public IntPtr hbmpItem;
	}

	public enum HBitmapHMenu : long {
		Callback = -1,
		MbarClose = 5,
		MbarCloseD = 6,
		MbarMinimize = 3,
		MbarMinimizeD = 7,
		MbarRestore = 2,
		PopupClose = 8,
		PopupMaximize = 10,
		PopupMinimize = 11,
		PopupRestore = 9,
		System = 1
	}

	[DllImport(User32, CharSet = CharSet.Unicode)]
	public static extern bool GetMenuItemInfo(IntPtr hmenu, uint item, bool fByPosition, ref MenuItemInfo lpmii);
	// ReSharper restore InconsistentNaming
	// ReSharper restore IdentifierTypo
	// ReSharper restore StringLiteralTypo
	// ReSharper restore UnusedMember.Global
	// ReSharper restore FieldCanBeMadeReadOnly.Local
	// ReSharper restore FieldCanBeMadeReadOnly.Global
	// ReSharper restore UnusedType.Global
#pragma warning restore CS0649
}