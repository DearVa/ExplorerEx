using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ExplorerProxy.Interop;
using Microsoft.Win32;
using SHDocVw;

namespace ExplorerProxy {
	/// <summary>
	/// 通过Shell拓展注册一个BHO，达到打开Windows File Explorer时自动打开ExplorerEx的效果
	/// </summary>
	[ComVisible(true), Guid("11451400-8700-480c-a27f-000001919810"), ClassInterface(ClassInterfaceType.None)]
	public class ExplorerExProxy : IObjectWithSite {
		private WebBrowserClass explorer;

		private const string RegKeyName = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects\";
		// ReSharper disable once InconsistentNaming
		private static Guid IID_IWebBrowserApp = new Guid("0002DF05-0000-0000-C000-000000000046");

		[ComRegisterFunction]
		public static void Register(Type t) {
			var name = t.GUID.ToString("B");
			try {
				using (var key = Registry.ClassesRoot.CreateSubKey(@"CLSID\" + name)) {
					if (key == null) {
						throw new UnauthorizedAccessException();
					}
					key.SetValue(null, "ExplorerExProxy");
					key.SetValue("MenuText", "ExplorerExProxy");
					key.SetValue("HelpText", "ExplorerExProxy");
				}
				Registry.LocalMachine.CreateSubKey(RegKeyName + name);
				MessageBox(IntPtr.Zero, "ExplorerEx Proxy Installed", "Info", MessageBoxType.IconInformation);
			} catch (Exception e) {
				MessageBox(IntPtr.Zero, "ExplorerEx Proxy Install Failed\n" + e, "Error", MessageBoxType.IconError);
			}
		}

		[ComUnregisterFunction]
		public static void Unregister(Type t) {
			try {
				using (var key = Registry.LocalMachine.CreateSubKey(RegKeyName)) {
					if (key == null) {
						throw new UnauthorizedAccessException();
					}
					key.DeleteSubKey(t.GUID.ToString("B"), false);
				}
				MessageBox(IntPtr.Zero, "ExplorerEx Proxy Uninstalled", "Info", MessageBoxType.IconInformation);
			} catch (Exception e) {
				MessageBox(IntPtr.Zero, "ExplorerEx Proxy Uninstall Failed\n" + e, "Error", MessageBoxType.IconError);
			}
		}

		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		public static extern int MessageBox(IntPtr hWnd, string text, string caption, MessageBoxType type);

		public enum MessageBoxType {
			Ok = 0x0,
			OkCancel = 0x1,
			AbortRetryIgnore = 0x2,
			YesNoCancel = 0x3,
			YesNo = 0x4,
			RetryCancel = 0x5,
			CancelTryContinue = 0x6,
			IconError = 0x10,
			IconQuestion = 0x20,
			IconWarning = 0x30,
			IconInformation = 0x40,
		}

		[DllImport("user32.dll")]
		public static extern bool CloseWindow(IntPtr hWnd);

		public void GetSite(ref Guid riid, out object ppvSite) {
			ppvSite = null;
		}

		public void SetSite(IntPtr pUnkSite) {
			if (Process.GetCurrentProcess().ProcessName != "explorer") {
				return;  // 过滤掉ie等其他程序
			}
			//if (Debugger.IsAttached) {
			//	Debugger.Break();
			//} else {
			//	Debugger.Launch();
			//}
			if (Marshal.QueryInterface(pUnkSite, ref IID_IWebBrowserApp, out var pExplorer) == 0) {
				// 文件管理器居然是IE套壳？？（大雾
				explorer = (WebBrowserClass)Marshal.GetTypedObjectForIUnknown(pExplorer, typeof(WebBrowserClass));
				explorer.NavigateComplete += Explorer_OnNavigateComplete;
			}
		}

		private void Explorer_OnNavigateComplete(string url) {
			explorer.NavigateComplete -= Explorer_OnNavigateComplete;
			if (string.IsNullOrWhiteSpace(url)) {
				OpenPathInExplorerEx(null);
			} else if (url.Length == 40) {
				// 可能是Shell位置
				if (url[0] == ':' && url[1] == ':' && url[2] == '{' && url[39] == '}') {
					if (Guid.TryParse(url.Substring(3, 36), out var clsId)) {
						if (clsId == new Guid(0x20D04FE0, 0x3AEA, 0x1069, 0xA2, 0xD8, 0x08, 0x00, 0x2B, 0x30, 0x30, 0x9D) ||
						    clsId == new Guid(0x5E5F29CE, 0xE0A8, 0x49D3, 0xAF, 0x32, 0x7A, 0x7B, 0xDC, 0x17, 0x34, 0x78)) {
							// This PC
							OpenPathInExplorerEx(null);
						}
					}
				}
			} else if (Directory.Exists(url)) {
				OpenPathInExplorerEx(url);
			}
		}

		private void OpenPathInExplorerEx(string path) {
			try {
				var mutex = Mutex.OpenExisting("ExplorerExMut");
				try {
					mutex.ReleaseMutex();
				} catch {
					// Ignore
				}
				try {
					mutex.Dispose();
				} catch {
					// Ignore
				}
				// 打开成功，说明ExplorerEx已启动，采用IPC
				using (var ipc = new ExplorerIpc("ExplorerExIPC", 1024)) {
					ipc.Write(Encoding.UTF8.GetBytes(path == null ? "Open" : "Open|" + path));
					explorer.Quit();
				}
			} catch {
				// ExplorerEx没有启动或者IPC通讯失败
				// 直接启动ExplorerEx
				using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Dear.Va\ExplorerEx", false)) {
					if (key?.GetValue("Path") is string explorerExPath && File.Exists(explorerExPath)) {
						try {
							Process.Start(new ProcessStartInfo(explorerExPath, path));
							explorer.Quit();
						} catch {
							// Ignore
						}
					}
				}
			}
		}
	}
}
