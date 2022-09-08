using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ExplorerProxy.Interop;
using Microsoft.Win32;
using SHDocVw;
using IServiceProvider = ExplorerProxy.Interop.IServiceProvider;

namespace ExplorerProxy {
	/// <summary>
	/// 通过Shell拓展注册一个BHO，达到打开Windows File Explorer时自动打开ExplorerEx的效果
	/// </summary>
	[ComVisible(true), Guid("11451400-8700-480c-a27f-000001919810"), ClassInterface(ClassInterfaceType.None)]
	public class ExplorerExProxy : IObjectWithSite {
		private IntPtr pUnkSite;
		private WebBrowserClass explorer;

		private const string RegKeyName = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects\";
		// ReSharper disable InconsistentNaming
		private static Guid IID_IWebBrowserApp = new Guid("0002DF05-0000-0000-C000-000000000046");
		private static Guid IID_IServiceProvider = new Guid("6d5140c1-7436-11ce-8034-00aa006009fa");
		private static Guid SID_STopLevelBrowser = new Guid(0x4C96BE40, 0x915C, 0x11CF, 0x99, 0xD3, 0x00, 0xAA, 0x00, 0x4A, 0xE8, 0x37);
		private static Guid IID_IShellBrowser = new Guid("000214E2-0000-0000-C000-000000000046");
		private static Guid IID_IFolderView = new Guid("cde725b0-ccc9-4519-917e-325d72fab4ce");
		private static Guid IID_IPersistFolder = new Guid("000214EA-0000-0000-C000-000000000046");
		private static Guid IID_IPersistFolder2 = new Guid("1AC3D9F0-175C-11d1-95BE-00609797EA4F");
		private static Guid IID_IShellFolder = new Guid("000214E6-0000-0000-C000-000000000046");
		// ReSharper restore InconsistentNaming

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
#if DEBUG
			if (Debugger.IsAttached) {
				Debugger.Break();
			} else {
				Debugger.Launch();
			}
#endif
			if (Marshal.QueryInterface(pUnkSite, ref IID_IWebBrowserApp, out var pExplorer) == 0) {
				// 文件管理器居然是IE套壳？？（大雾
				this.pUnkSite = pUnkSite;
				explorer = (WebBrowserClass)Marshal.GetTypedObjectForIUnknown(pExplorer, typeof(WebBrowserClass));
				explorer.DocumentComplete += Explorer_OnDocumentComplete;
				explorer.WindowStateChanged += ExplorerOnWindowStateChanged;
			}
		}

		private void ExplorerOnWindowStateChanged(uint dwwindowstateflags, uint dwvalidflagsmask) {
			
		}

		private void Explorer_OnDocumentComplete(object pdisp, ref object url) {
			explorer.DocumentComplete -= Explorer_OnDocumentComplete;
			var urlStr = (string)url;
			if (string.IsNullOrWhiteSpace(urlStr)) {
				OpenPathInExplorerEx(null);
			} else if (urlStr.Length == 40) {
				// 可能是Shell位置
				if (urlStr[0] == ':' && urlStr[1] == ':' && urlStr[2] == '{' && urlStr[39] == '}') {
					if (Guid.TryParse(urlStr.Substring(3, 36), out var clsId)) {
						if (clsId == new Guid(0x20D04FE0, 0x3AEA, 0x1069, 0xA2, 0xD8, 0x08, 0x00, 0x2B, 0x30, 0x30, 0x9D) ||
							clsId == new Guid(0x5E5F29CE, 0xE0A8, 0x49D3, 0xAF, 0x32, 0x7A, 0x7B, 0xDC, 0x17, 0x34, 0x78)) {
							// This PC
							OpenPathInExplorerEx(null);
						}
					}
				}
			} else if (Directory.Exists(urlStr)) {
				OpenPathInExplorerEx(urlStr);
			}
		}

		private void Do() {
			if (Marshal.QueryInterface(pUnkSite, ref IID_IServiceProvider, out var psp) != 0) {
				return;
			}
			var sp = (IServiceProvider)Marshal.GetTypedObjectForIUnknown(psp, typeof(IServiceProvider));
			if (sp.QueryService(ref SID_STopLevelBrowser, ref IID_IShellBrowser, out var psb) != 0) {
				Marshal.ReleaseComObject(sp);
				return;
			}
			Marshal.ReleaseComObject(sp);
			var sb = (IShellBrowser)psb;
			if (sb.QueryActiveShellView(out var psv) != 0) {
				Marshal.ReleaseComObject(sb);
				return;
			}
			Marshal.ReleaseComObject(sb);
			Marshal.QueryInterface(psv, ref IID_IFolderView, out var pfv);
			var fv = (IFolderView)Marshal.GetTypedObjectForIUnknown(pfv, typeof(IFolderView));
			fv.GetFolder(ref IID_IPersistFolder, out var ppf);
			fv.GetFocusedItem(out var idxFocus);
			fv.Item(idxFocus, out var pidlItem);
			Marshal.QueryInterface(ppf, ref IID_IShellFolder, out var psf);
			var sf = (IShellFolder)Marshal.GetTypedObjectForIUnknown(psf, typeof(IShellFolder));
			sf.GetDisplayNameOf(pidlItem, SHGDNF.SHGDN_INFOLDER, out var name);
			var fileName = Marshal.PtrToStringUni(name.data);
		}

		private void OpenPathInExplorerEx(string path) {
			try {
				using (var mutex = Mutex.OpenExisting("ExplorerExMut")) {
					try {
						mutex.ReleaseMutex();
					} catch {
						// Ignore
					}
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
							Process.Start(new ProcessStartInfo(explorerExPath, '"' + path + '"'));
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
