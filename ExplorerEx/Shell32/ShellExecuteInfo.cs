using System;
using System.Runtime.InteropServices;

namespace ExplorerEx.Shell32; 

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public struct ShellExecuteInfo {
	public int cbSize;
	public uint fMask;
	public IntPtr hwnd;
	[MarshalAs(UnmanagedType.LPTStr)]
	public string lpVerb;
	[MarshalAs(UnmanagedType.LPTStr)]
	public string lpFile;
	[MarshalAs(UnmanagedType.LPTStr)]
	public string lpParameters;
	[MarshalAs(UnmanagedType.LPTStr)]
	public string lpDirectory;
	public int nShow;
	public IntPtr hInstApp;
	public IntPtr lpIDList;
	[MarshalAs(UnmanagedType.LPTStr)]
	public string lpClass;
	public IntPtr hkeyClass;
	public uint dwHotKey;
	public IntPtr hIcon;
	public IntPtr hProcess;
}