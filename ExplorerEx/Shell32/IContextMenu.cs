using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ExplorerEx.Shell32;

/// <summary>
/// Flags that specify how the shortcut menu can be changed.
/// </summary>
[Flags]
// ReSharper disable once InconsistentNaming
public enum CMF : uint {
    /// <summary>
    /// Indicates normal operation. A shortcut menu extension, namespace extension, or drag-and-drop handler can add all menu items.
    /// </summary>
    Normal = 0x00000000,

    /// <summary>
    /// The user is activating the default action, typically by double-clicking. This flag provides a hint for the shortcut menu extension to add nothing if it does not modify the default item in the menu. A shortcut menu extension or drag-and-drop handler should not add any menu items if this value is specified. A namespace extension should at most add only the default item.
    /// </summary>
    DefaultOnly = 0x00000001,

    /// <summary>
    /// The shortcut menu is that of a shortcut file (normally, a .lnk file). Shortcut menu handlers should ignore this value.
    /// </summary>
    VerbsOnly = 0x00000002,

    /// <summary>
    /// The Windows Explorer tree window is present.
    /// </summary>
    Explore = 0x00000004,

    /// <summary>
    /// This flag is set for items displayed in the Send To menu. Shortcut menu handlers should ignore this value.
    /// </summary>
    NoVerbs = 0x00000008,

    /// <summary>
    /// The calling application supports renaming of items. A shortcut menu or drag-and-drop handler should ignore this flag. A namespace extension should add a Rename item to the menu if applicable.
    /// </summary>
    CanRename = 0x00000010,

    /// <summary>
    /// No item in the menu has been set as the default. A drag-and-drop handler should ignore this flag. A namespace extension should not set any of the menu items as the default.
    /// </summary>
    NoDefault = 0x00000020,

    /// <summary>
    /// A static menu is being constructed. Only the browser should use this flag; all other shortcut menu extensions should ignore it.
    /// </summary>
    IncludeStatic = 0x00000040,

    /// <summary>
    /// The calling application is invoking a shortcut menu on an item in the view (as opposed to the background of the view).
    /// Windows Server 2003 and Windows XP:  This value is not available.
    /// </summary>
    ItemMenu = 0x00000080,

    /// <summary>
    /// The calling application wants extended verbs. Normal verbs are displayed when the user right-clicks an object. To display extended verbs, the user must right-click while pressing the Shift key.
    /// </summary>
    ExtendedVerbs = 0x00000100,

    /// <summary>
    /// The calling application intends to invoke verbs that are disabled, such as legacy menus.
    /// Windows Server 2003 and Windows XP:  This value is not available.
    /// </summary>
    DisabledVerbs = 0x00000200,

    /// <summary>
    /// The verb state can be evaluated asynchronously.
    /// Windows Server 2008, Windows Vista, Windows Server 2003, and Windows XP:  This value is not available.
    /// </summary>
    AsyncVerbState = 0x00000400,

    /// <summary>
    /// Informs context menu handlers that do not support the invocation of a verb through a canonical verb name to bypass IContextMenu::QueryContextMenu in their implementation.
    ///     Windows Server 2008, Windows Vista, Windows Server 2003, and Windows XP:  This value is not available.
    /// </summary>
    OptimizeForInvoke = 0x00000800,

    /// <summary>
    /// Populate submenus synchronously.
    ///     Windows Server 2008, Windows Vista, Windows Server 2003, and Windows XP:  This value is not available.
    /// </summary>
    SyncCascadeMenu = 0x00001000,

    /// <summary>
    /// When no verb is explicitly specified, do not use a default verb in its place.
    ///     Windows Server 2008, Windows Vista, Windows Server 2003, and Windows XP:  This value is not available.
    /// </summary>
    DoNotPickDefault = 0x00002000,

    /// <summary>
    /// This flag is a bitmask that specifies all bits that should not be used. This is to be used only as a mask. Do not pass this as a parameter value.
    /// </summary>
    Reserved = 0xFFFF0000
}

/// <summary>
/// Flags specifying the information to return. This parameter can have one of the following values.
/// </summary>
[Flags]
// ReSharper disable once InconsistentNaming
public enum GCS : uint {
	/// <summary>
	/// Sets pszName to an ANSI string containing the language-independent command name for the menu item.
	/// </summary>
	VerbA = 0x00000000,

	/// <summary>
	/// Sets pszName to an ANSI string containing the help text for the command.
	/// </summary>
	HelpTextA = 0x00000001,

	/// <summary>
	/// Returns S_OK if the menu item exists, or S_FALSE otherwise.
	/// </summary>
	ValidateA = 0x00000002,

	/// <summary>
	/// Sets pszName to a Unicode string containing the language-independent command name for the menu item.
	/// </summary>
	VerbW = 0x00000004,

	/// <summary>
	/// Sets pszName to a Unicode string containing the help text for the command.
	/// </summary>
	HelpTextW = 0x00000005,

	/// <summary>
	/// Returns S_OK if the menu item exists, or S_FALSE otherwise.
	/// </summary>
	ValidateW = 0x00000006,

	/// <summary>
	/// Not documented.
	/// </summary>
	VerbIconW = 0x00000014,

	/// <summary>
	/// Not documented.
	/// </summary>
	Unicode = 0x00000004
}

[StructLayout(LayoutKind.Sequential)]
public struct CtxMenuInvokeCommandInfo {
	public int cbSize;
	public int fMask;
	public IntPtr hwnd;
    public IntPtr lpVerb;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string? lpParameters;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string? lpDirectory;
	public int nShow;
	public int dwHotKey;
	public IntPtr hIcon;
}

/// <summary>
/// Exposes methods that either create or merge a shortcut menu associated with a Shell object.
/// </summary>
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid(Shell32Interop.IID_IContextMenu)]
public interface IContextMenu {
	/// <summary>
	/// Adds commands to a shortcut menu.
	/// </summary>
	/// <param name="hMenu">A handle to the shortcut menu. The handler should specify this handle when adding menu items.</param>
	/// <param name="indexMenu">The zero-based position at which to insert the first new menu item.</param>
	/// <param name="idCmdFirst">The minimum value that the handler can specify for a menu item identifier.</param>
	/// <param name="idCmdLast">The maximum value that the handler can specify for a menu item identifier.</param>
	/// <param name="uFlags">Optional flags that specify how the shortcut menu can be changed. This parameter can be set to a combination of the following values. The remaining bits of the low-order word are reserved by the system. The high-order word can be used for context-specific communications. The CMF_RESERVED value can be used to mask the low-order word.</param>
	/// <returns>If successful, returns an HRESULT value that has its severity value set to SEVERITY_SUCCESS and its code value set to the offset of the largest command identifier that was assigned, plus one. For example, if idCmdFirst is set to 5 and you add three items to the menu with command identifiers of 5, 7, and 8, the return value should be MAKE_HRESULT(SEVERITY_SUCCESS, 0, 8 - 5 + 1). Otherwise, it returns a COM error value.</returns>
	[PreserveSig]
	int QueryContextMenu(IntPtr hMenu, uint indexMenu, int idCmdFirst, int idCmdLast, CMF uFlags);

	/// <summary>
	/// Carries out the command associated with a shortcut menu item.
	/// </summary>
	/// <param name="pici">A pointer to a CMINVOKECOMMANDINFO or CMINVOKECOMMANDINFOEX structure containing information about the command. For further details, see the Remarks section.</param>
	/// <returns>If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.</returns>
	[PreserveSig]
	int InvokeCommand(CtxMenuInvokeCommandInfo pici);

	/// <summary>
	/// Gets information about a shortcut menu command, including the help string and the language-independent, or canonical, name for the command.
	/// </summary>
	/// <param name="idCmd">Menu command identifier offset.</param>
	/// <param name="uFlags">Flags specifying the information to return. This parameter can have one of the following values.</param>
	/// <param name="reserved">Reserved. Applications must specify NULL when calling this method and handlers must ignore this parameter when called.</param>
	/// <param name="commandString">The address of the buffer to receive the null-terminated string being retrieved.</param>
	/// <param name="cch">Size of the buffer, in characters, to receive the null-terminated string.</param>
	/// <returns>If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.</returns>
	[PreserveSig]
	int GetCommandString(int idCmd, GCS uFlags, int reserved, StringBuilder commandString, int cch);
}

/// <summary>
/// Exposes methods that either create or merge a shortcut (context) menu associated with a Shell object. Extends IContextMenu by adding a method that allows client objects to handle messages associated with owner-drawn menu items.
/// </summary>
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("000214f4-0000-0000-c000-000000000046")]
public interface IContextMenu2 : IContextMenu {
    #region IContextMenu overrides

    /// <summary>
    /// Adds commands to a shortcut menu.
    /// </summary>
    /// <param name="hMenu">A handle to the shortcut menu. The handler should specify this handle when adding menu items.</param>
    /// <param name="indexMenu">The zero-based position at which to insert the first new menu item.</param>
    /// <param name="idCmdFirst">The minimum value that the handler can specify for a menu item identifier.</param>
    /// <param name="idCmdLast">The maximum value that the handler can specify for a menu item identifier.</param>
    /// <param name="uFlags">Optional flags that specify how the shortcut menu can be changed. This parameter can be set to a combination of the following values. The remaining bits of the low-order word are reserved by the system. The high-order word can be used for context-specific communications. The CMF_RESERVED value can be used to mask the low-order word.</param>
    /// <returns>
    /// If successful, returns an HRESULT value that has its severity value set to SEVERITY_SUCCESS and its code value set to the offset of the largest command identifier that was assigned, plus one. For example, if idCmdFirst is set to 5 and you add three items to the menu with command identifiers of 5, 7, and 8, the return value should be MAKE_HRESULT(SEVERITY_SUCCESS, 0, 8 - 5 + 1). Otherwise, it returns a COM error value.
    /// </returns>
    new int QueryContextMenu(IntPtr hMenu, uint indexMenu, int idCmdFirst, int idCmdLast, CMF uFlags);

    /// <summary>
    /// Carries out the command associated with a shortcut menu item.
    /// </summary>
    /// <param name="pici">A pointer to a CMINVOKECOMMANDINFO or CMINVOKECOMMANDINFOEX structure containing information about the command. For further details, see the Remarks section.</param>
    /// <returns>
    /// If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.
    /// </returns>
    new int InvokeCommand(CtxMenuInvokeCommandInfo pici);

    /// <summary>
    /// Gets information about a shortcut menu command, including the help string and the language-independent, or canonical, name for the command.
    /// </summary>
    /// <param name="idCmd">Menu command identifier offset.</param>
    /// <param name="uFlags">Flags specifying the information to return. This parameter can have one of the following values.</param>
    /// <param name="reserved">Reserved. Applications must specify NULL when calling this method and handlers must ignore this parameter when called.</param>
    /// <param name="commandString">The address of the buffer to receive the null-terminated string being retrieved.</param>
    /// <param name="cch">Size of the buffer, in characters, to receive the null-terminated string.</param>
    /// <returns>
    /// If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.
    /// </returns>
    new int GetCommandString(int idCmd, GCS uFlags, int reserved, StringBuilder commandString, int cch);

    #endregion

    /// <summary>
    /// Enables client objects of the IContextMenu interface to handle messages associated with owner-drawn menu items.
    /// </summary>
    /// <param name="uMsg">The message to be processed. In the case of some messages, such as WM_INITMENUPOPUP, WM_DRAWITEM, WM_MENUCHAR, or WM_MEASUREITEM, the client object being called may provide owner-drawn menu items.</param>
    /// <param name="wParam">Additional message information. The value of this parameter depends on the value of the uMsg parameter.</param>
    /// <param name="lParam">Additional message information. The value of this parameter depends on the value of the uMsg parameter.</param>
    /// <returns>If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.</returns>
    [PreserveSig]
    int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
}

/// <summary>
/// Exposes methods that either create or merge a shortcut menu associated with a Shell object. Allows client objects to handle messages associated with owner-drawn menu items and extends IContextMenu2 by accepting a return value from that message handling.
/// </summary>
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("BCFCE0A0-EC17-11d0-8D10-00A0C90F2719")]
public interface IContextMenu3 : IContextMenu2 {
    #region IContextMenu and IContextMenu2 overrides

    /// <summary>
    /// Adds commands to a shortcut menu.
    /// </summary>
    /// <param name="hMenu">A handle to the shortcut menu. The handler should specify this handle when adding menu items.</param>
    /// <param name="indexMenu">The zero-based position at which to insert the first new menu item.</param>
    /// <param name="idCmdFirst">The minimum value that the handler can specify for a menu item identifier.</param>
    /// <param name="idCmdLast">The maximum value that the handler can specify for a menu item identifier.</param>
    /// <param name="uFlags">Optional flags that specify how the shortcut menu can be changed. This parameter can be set to a combination of the following values. The remaining bits of the low-order word are reserved by the system. The high-order word can be used for context-specific communications. The CMF_RESERVED value can be used to mask the low-order word.</param>
    /// <returns>
    /// If successful, returns an HRESULT value that has its severity value set to SEVERITY_SUCCESS and its code value set to the offset of the largest command identifier that was assigned, plus one. For example, if idCmdFirst is set to 5 and you add three items to the menu with command identifiers of 5, 7, and 8, the return value should be MAKE_HRESULT(SEVERITY_SUCCESS, 0, 8 - 5 + 1). Otherwise, it returns a COM error value.
    /// </returns>
    new int QueryContextMenu(IntPtr hMenu, uint indexMenu, int idCmdFirst, int idCmdLast, CMF uFlags);

    /// <summary>
    /// Carries out the command associated with a shortcut menu item.
    /// </summary>
    /// <param name="pici">A pointer to a CMINVOKECOMMANDINFO or CMINVOKECOMMANDINFOEX structure containing information about the command. For further details, see the Remarks section.</param>
    /// <returns>
    /// If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.
    /// </returns>
    new int InvokeCommand(CtxMenuInvokeCommandInfo pici);

    /// <summary>
    /// Gets information about a shortcut menu command, including the help string and the language-independent, or canonical, name for the command.
    /// </summary>
    /// <param name="idCmd">Menu command identifier offset.</param>
    /// <param name="uFlags">Flags specifying the information to return. This parameter can have one of the following values.</param>
    /// <param name="reserved">Reserved. Applications must specify NULL when calling this method and handlers must ignore this parameter when called.</param>
    /// <param name="commandString">The address of the buffer to receive the null-terminated string being retrieved.</param>
    /// <param name="cch">Size of the buffer, in characters, to receive the null-terminated string.</param>
    /// <returns>
    /// If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.
    /// </returns>
    new int GetCommandString(int idCmd, GCS uFlags, int reserved, StringBuilder commandString, int cch);

    /// <summary>
    /// Enables client objects of the IContextMenu interface to handle messages associated with owner-drawn menu items.
    /// </summary>
    /// <param name="uMsg">The message to be processed. In the case of some messages, such as WM_INITMENUPOPUP, WM_DRAWITEM, WM_MENUCHAR, or WM_MEASUREITEM, the client object being called may provide owner-drawn menu items.</param>
    /// <param name="wParam">Additional message information. The value of this parameter depends on the value of the uMsg parameter.</param>
    /// <param name="lParam">Additional message information. The value of this parameter depends on the value of the uMsg parameter.</param>
    /// <returns>
    /// If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.
    /// </returns>
    new int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);

    #endregion

    /// <summary>
    /// Allows client objects of the IContextMenu3 interface to handle messages associated with owner-drawn menu items.
    /// </summary>
    /// <param name="uMsg">The message to be processed. In the case of some messages, such as WM_INITMENUPOPUP, WM_DRAWITEM, WM_MENUCHAR, or WM_MEASUREITEM, the client object being called may provide owner-drawn menu items.</param>
    /// <param name="wParam">Additional message information. The value of this parameter depends on the value of the uMsg parameter.</param>
    /// <param name="lParam">Additional message information. The value of this parameter depends on the value of the uMsg parameter.</param>
    /// <param name="plResult">The address of an LRESULT value that the owner of the menu will return from the message. This parameter can be NULL.</param>
    /// <returns>If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.</returns>
    [PreserveSig]
    int HandleMenuMsg2(uint uMsg, IntPtr wParam, IntPtr lParam, ref IntPtr plResult);
}
