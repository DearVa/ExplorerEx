using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ExplorerEx.Shell32; 

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid(Shell32Interop.IID_IShellLink)]
interface IShellLinkW {
	/// <summary>Retrieves the path and file name of a Shell link object</summary>
	void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, SLGP fFlags);
	/// <summary>Retrieves the list of item identifiers for a Shell link object</summary>
	void GetIDList(out IntPtr ppidl);
	/// <summary>Sets the pointer to an item identifier list (PIDL) for a Shell link object.</summary>
	void SetIDList(IntPtr pidl);
	/// <summary>Retrieves the description string for a Shell link object</summary>
	void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
	/// <summary>Sets the description for a Shell link object. The description can be any application-defined string</summary>
	void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
	/// <summary>Retrieves the name of the working directory for a Shell link object</summary>
	void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
	/// <summary>Sets the name of the working directory for a Shell link object</summary>
	void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
	/// <summary>Retrieves the command-line arguments associated with a Shell link object</summary>
	void GetArguments([Out(), MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
	/// <summary>Sets the command-line arguments for a Shell link object</summary>
	void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
	/// <summary>Retrieves the hot key for a Shell link object</summary>
	void GetHotkey(out short pwHotkey);
	/// <summary>Sets a hot key for a Shell link object</summary>
	void SetHotkey(short wHotkey);
	/// <summary>Retrieves the show command for a Shell link object</summary>
	void GetShowCmd(out int piShowCmd);
	/// <summary>Sets the show command for a Shell link object. The show command sets the initial show state of the window.</summary>
	void SetShowCmd(int iShowCmd);
	/// <summary>Retrieves the location (path and index) of the icon for a Shell link object</summary>
	void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath,
		int cchIconPath, out int piIcon);
	/// <summary>Sets the location (path and index) of the icon for a Shell link object</summary>
	void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
	/// <summary>Sets the relative path to the Shell link object</summary>
	void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
	/// <summary>Attempts to find the target of a Shell link, even if it has been moved or renamed</summary>
	void Resolve(IntPtr hwnd, SLR f);
	/// <summary>Sets the path and file name of a Shell link object</summary>
	void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
}

[Flags]
enum SLR {
	/// <summary>
	/// Do not display a dialog box if the link cannot be resolved. When SLR_NO_UI is set,
	/// the high-order word of fFlags can be set to a time-out value that specifies the
	/// maximum amount of time to be spent resolving the link. The function returns if the
	/// link cannot be resolved within the time-out duration. If the high-order word is set
	/// to zero, the time-out duration will be set to the default value of 3,000 milliseconds
	/// (3 seconds). To specify a value, set the high word of fFlags to the desired time-out
	/// duration, in milliseconds.
	/// </summary>
	NoUI = 0x1,
	/// <summary>Obsolete and no longer used</summary>
	AnyMatch = 0x2,
	/// <summary>If the link object has changed, update its path and list of identifiers.
	/// If SLR_UPDATE is set, you do not need to call IPersistFile::IsDirty to determine
	/// whether or not the link object has changed.</summary>
	Update = 0x4,
	/// <summary>Do not update the link information</summary>
	NoUpdate = 0x8,
	/// <summary>Do not execute the search heuristics</summary>
	NoSearch = 0x10,
	/// <summary>Do not use distributed link tracking</summary>
	NoTrack = 0x20,
	/// <summary>Disable distributed link tracking. By default, distributed link tracking tracks
	/// removable media across multiple devices based on the volume name. It also uses the
	/// Universal Naming Convention (UNC) path to track remote file systems whose drive letter
	/// has changed. Setting SLR_NOLINKINFO disables both types of tracking.</summary>
	NoLinking = 0x40,
	/// <summary>Call the Microsoft Windows Installer</summary>
	InvokeMsi = 0x80
}

[Flags]
enum SLGP {
	/// <summary>Retrieves the standard short (8.3 format) file name</summary>
	ShortPath = 0x1,
	/// <summary>Retrieves the Universal Naming Convention (UNC) path name of the file</summary>
	UncPriority = 0x2,
	/// <summary>Retrieves the raw path name. A raw path is something that might not exist and may include environment variables that need to be expanded</summary>
	RawPath = 0x4
}