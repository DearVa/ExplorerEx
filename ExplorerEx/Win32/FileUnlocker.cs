using System;
using System.Diagnostics;
using static ExplorerEx.Win32.Win32Interop;

namespace ExplorerEx.Win32;

/// <summary>
/// 提供解锁占用文件的类
/// </summary>
public static class FileUnlocker {
	/// <summary>
	/// 通过RestartManage获取占用文件的进程pid列表，这个方法无法获取内存文件映射而占用的程序
	/// </summary>
	/// <param name="fullPaths"></param>
	/// <returns></returns>
	/// <exception cref="Exception"></exception>
	public static uint[]? GetFileOccupiedPidList(params string[] fullPaths) {
		uint[]? pidList = null;

		var res = RmStartSession(out var handle, 0, Guid.NewGuid().ToString());

		if (res != 0) {
			throw new Exception("Could not begin restart session. Unable to determine file locker.");
		}

		try {
			uint pnProcInfo = 0, rebootReasons = 0;

			res = RmRegisterResources(handle, (uint)fullPaths.Length, fullPaths, 0, null, 0, null);

			if (res != 0) {
				throw new Exception("Could not register resource.");
			}

			//Note: there's a race condition here -- the first call to RmGetList() returns
			//      the total number of process. However, when we call RmGetList() again to get
			//      the actual processes this number may have increased.
			res = RmGetList(handle, out var pnProcInfoNeeded, ref pnProcInfo, null, ref rebootReasons);

			if (res == 234) {  // ErrorMoreData
				// Create an array to store the process results
				var processInfo = new RmProcessInfo[pnProcInfoNeeded];
				pnProcInfo = pnProcInfoNeeded;

				// Get the list
				res = RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, processInfo, ref rebootReasons);

				if (res == 0) {
					pidList = new uint[pnProcInfo];

					// Enumerate all of the results and add them to the 
					// list to be returned
					for (var i = 0; i < pnProcInfo; i++) {
						pidList[i] = processInfo[i].Process.dwProcessId;
					}
				} else {
					throw new Exception("Could not list processes locking resource.");
				}
			} else if (res != 0) {
				throw new Exception("Could not list processes locking resource. Failed to get size of result.");
			}
		} finally {
			RmEndSession(handle);
		}

		return pidList;
	}

	public static void Unlock(string fullPath) {
		// 先通过RestartManager快速找出有哪些进程占用了目标文件
		var pidList = GetFileOccupiedPidList(fullPath);
		if (pidList == null) {
			return;
		}
		var hCurrentProcess = Process.GetCurrentProcess().Handle;
		foreach (var pid in pidList) {
			// 找到占用该文件的pid列表之后，逐个打开
			var hProcess = OpenProcess(OpenProcessDesiredAccess.ProcessDupHandle, false, pid);
			if (hProcess == IntPtr.Zero) {
				throw new UnauthorizedAccessException();  // 无法打开目标进程，可能权限不够
			}
			var handles = SearchFileHandles(pid, hProcess, fullPath);  // 有了pid作为过滤器，这个方法速度是很快的
			foreach (var handle in handles) {
				// 复制句柄！但是Option为CloseSource，关闭原句柄
				DuplicateHandle(hProcess, handle, hCurrentProcess, out var duplicatedHandle, 0, false, 1);
				// 之后关闭复制的句柄，搞定
				CloseHandle(duplicatedHandle);
			}
		}
	}
}