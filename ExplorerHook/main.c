#include <Windows.h>
#include <Psapi.h>

BOOL IsExplorerRunning() {
	DWORD pidList[1024], cbNeeded;

	if (!EnumProcesses(pidList, sizeof(pidList), &cbNeeded)) {
		return FALSE;
	}
	const DWORD pidCount = min(cbNeeded / sizeof(DWORD), 1024);
	for (unsigned int i = 0; i < pidCount; i++) {
		if (pidList[i] != 0) {
			HANDLE hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, pidList[i]);
			if (hProcess != NULL) {
				HMODULE hMod;
				if (EnumProcessModules(hProcess, &hMod, sizeof(HMODULE), &cbNeeded)) {
					TCHAR szProcessName[MAX_PATH];
					const DWORD length = GetModuleBaseName(hProcess, hMod, szProcessName, sizeof(szProcessName) / sizeof(TCHAR));
					if (length == 12 &&
						(szProcessName[0] == 'E' || szProcessName[0] == 'e') &&
						(szProcessName[1] == 'X' || szProcessName[1] == 'x') &&
						(szProcessName[2] == 'P' || szProcessName[2] == 'p') &&
						(szProcessName[3] == 'L' || szProcessName[3] == 'l') &&
						(szProcessName[4] == 'O' || szProcessName[4] == 'o') &&
						(szProcessName[5] == 'R' || szProcessName[5] == 'r') &&
						(szProcessName[6] == 'E' || szProcessName[6] == 'e') &&
						(szProcessName[7] == 'R' || szProcessName[7] == 'r') &&
						szProcessName[8] == '.' &&
						(szProcessName[9] == 'E' || szProcessName[8] == 'e') &&
						(szProcessName[10] == 'X' || szProcessName[10] == 'x') &&
						(szProcessName[11] == 'E' || szProcessName[11] == 'e')) {
						return TRUE;
					}
				}
			}
		}
	}

	return FALSE;
}

void RunExplorerEx() {
	WinExec("G:\\Source\\C#\\ExplorerEx\\ExplorerEx\\bin\\x64\\Release\\net6.0-windows\\win-x64\\ExplorerHook.exe", SW_NORMAL);
}

void RunExplorer() {
	WinExec("C:\\Windows\\EXPLORER.EXE", SW_NORMAL);
}

// 使用映像劫持Hook系统自带Explorer到ExplorerEx
int main(int argc, char *argv[]) {
	if (argc == 1) {  // 没有参数
		if (IsExplorerRunning()) {
			RunExplorerEx();
		} else {
			RunExplorer();
		}
	} else if (strncmp(argv[1], "/factory", 8) == 0) {
		RunExplorer();
	} else {
		RunExplorerEx();
	}
}