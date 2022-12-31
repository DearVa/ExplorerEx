using System;
using System.Windows.Interop;

namespace ExplorerEx.Views;

public class WindowWrapper : IWin32Window {
	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="handle">Handle to wrap</param>
	public WindowWrapper(IntPtr handle) {
		Handle = handle;
	}

	/// <summary>
	/// Original ptr
	/// </summary>
	public IntPtr Handle { get; }
}