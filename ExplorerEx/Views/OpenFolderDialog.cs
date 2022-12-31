using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using ExplorerEx.Shell32;

namespace ExplorerEx.Views;
public class OpenFolderDialog {
	/// <summary>
	/// Gets/sets folder in which dialog will be open.
	/// </summary>
	public string? InitialFolder { get; set; }

	/// <summary>
	/// Gets/sets directory in which dialog will be open if there is no recent directory available.
	/// </summary>
	public string? DefaultFolder { get; set; }

	/// <summary>
	/// Gets selected folder.
	/// </summary>
	public string? Folder { get; set; }

	public OpenFolderDialog() { }

	public OpenFolderDialog(string initialFolder) {
		InitialFolder = initialFolder;
	}

	public bool ShowDialog() {
		return ShowDialog(new WindowWrapper(IntPtr.Zero));
	}

	public bool ShowDialog(IWin32Window owner) {
		// ReSharper disable once SuspiciousTypeConversion.Global
		var frm = (IFileDialog)new FileOpenDialogRCW();
		frm.GetOptions(out var options);
		options |= FOS.PickFolders | FOS.ForceFileSystem | FOS.NoValidate | FOS.NoTestFileCreate | FOS.DontAddToRecent;
		frm.SetOptions(options);
		if (InitialFolder != null) {
			var riid = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"); //IShellItem
			if (Shell32Interop.SHCreateItemFromParsingName(InitialFolder, null, riid, out var directoryShellItem) == 0) {
				frm.SetFolder(directoryShellItem);
			}
		}
		if (DefaultFolder != null) {
			var riid = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"); //IShellItem
			if (Shell32Interop.SHCreateItemFromParsingName(DefaultFolder, null, riid, out var directoryShellItem) == 0) {
				frm.SetDefaultFolder(directoryShellItem);
			}
		}

		if (frm.Show(owner.Handle) == 0) {
			if (frm.GetResult(out var shellItem) == 0) {
				if (shellItem.GetDisplayName(SIGDN.FileSysPath, out var pszString) == 0) {
					if (pszString != IntPtr.Zero) {
						try {
							Folder = Marshal.PtrToStringAuto(pszString);
							return true;
						} finally {
							Marshal.FreeCoTaskMem(pszString);
						}
					}
				}
			}
		}
		return false;
	}

	[ComImport, ClassInterface(ClassInterfaceType.None), TypeLibType(TypeLibTypeFlags.FCanCreate), Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
	internal class FileOpenDialogRCW { }
}
