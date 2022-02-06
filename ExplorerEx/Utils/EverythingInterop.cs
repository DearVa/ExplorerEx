using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ExplorerEx.Utils; 

internal class EverythingInterop {
	private const int EVERYTHING_OK = 0;
	private const int EVERYTHING_ERROR_MEMORY = 1;
	private const int EVERYTHING_ERROR_IPC = 2;
	private const int EVERYTHING_ERROR_REGISTERCLASSEX = 3;
	private const int EVERYTHING_ERROR_CREATEWINDOW = 4;
	private const int EVERYTHING_ERROR_CREATETHREAD = 5;
	private const int EVERYTHING_ERROR_INVALIDINDEX = 6;
	private const int EVERYTHING_ERROR_INVALIDCALL = 7;

	private const int EVERYTHING_REQUEST_FILE_NAME = 0x00000001;
	private const int EVERYTHING_REQUEST_PATH = 0x00000002;
	private const int EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME = 0x00000004;
	private const int EVERYTHING_REQUEST_EXTENSION = 0x00000008;
	private const int EVERYTHING_REQUEST_SIZE = 0x00000010;
	private const int EVERYTHING_REQUEST_DATE_CREATED = 0x00000020;
	private const int EVERYTHING_REQUEST_DATE_MODIFIED = 0x00000040;
	private const int EVERYTHING_REQUEST_DATE_ACCESSED = 0x00000080;
	private const int EVERYTHING_REQUEST_ATTRIBUTES = 0x00000100;
	private const int EVERYTHING_REQUEST_FILE_LIST_FILE_NAME = 0x00000200;
	private const int EVERYTHING_REQUEST_RUN_COUNT = 0x00000400;
	private const int EVERYTHING_REQUEST_DATE_RUN = 0x00000800;
	private const int EVERYTHING_REQUEST_DATE_RECENTLY_CHANGED = 0x00001000;
	private const int EVERYTHING_REQUEST_HIGHLIGHTED_FILE_NAME = 0x00002000;
	private const int EVERYTHING_REQUEST_HIGHLIGHTED_PATH = 0x00004000;
	private const int EVERYTHING_REQUEST_HIGHLIGHTED_FULL_PATH_AND_FILE_NAME = 0x00008000;

	private const int EVERYTHING_SORT_NAME_ASCENDING = 1;
	private const int EVERYTHING_SORT_NAME_DESCENDING = 2;
	private const int EVERYTHING_SORT_PATH_ASCENDING = 3;
	private const int EVERYTHING_SORT_PATH_DESCENDING = 4;
	private const int EVERYTHING_SORT_SIZE_ASCENDING = 5;
	private const int EVERYTHING_SORT_SIZE_DESCENDING = 6;
	private const int EVERYTHING_SORT_EXTENSION_ASCENDING = 7;
	private const int EVERYTHING_SORT_EXTENSION_DESCENDING = 8;
	private const int EVERYTHING_SORT_TYPE_NAME_ASCENDING = 9;
	private const int EVERYTHING_SORT_TYPE_NAME_DESCENDING = 10;
	private const int EVERYTHING_SORT_DATE_CREATED_ASCENDING = 11;
	private const int EVERYTHING_SORT_DATE_CREATED_DESCENDING = 12;
	private const int EVERYTHING_SORT_DATE_MODIFIED_ASCENDING = 13;
	private const int EVERYTHING_SORT_DATE_MODIFIED_DESCENDING = 14;
	private const int EVERYTHING_SORT_ATTRIBUTES_ASCENDING = 15;
	private const int EVERYTHING_SORT_ATTRIBUTES_DESCENDING = 16;
	private const int EVERYTHING_SORT_FILE_LIST_FILENAME_ASCENDING = 17;
	private const int EVERYTHING_SORT_FILE_LIST_FILENAME_DESCENDING = 18;
	private const int EVERYTHING_SORT_RUN_COUNT_ASCENDING = 19;
	private const int EVERYTHING_SORT_RUN_COUNT_DESCENDING = 20;
	private const int EVERYTHING_SORT_DATE_RECENTLY_CHANGED_ASCENDING = 21;
	private const int EVERYTHING_SORT_DATE_RECENTLY_CHANGED_DESCENDING = 22;
	private const int EVERYTHING_SORT_DATE_ACCESSED_ASCENDING = 23;
	private const int EVERYTHING_SORT_DATE_ACCESSED_DESCENDING = 24;
	private const int EVERYTHING_SORT_DATE_RUN_ASCENDING = 25;
	private const int EVERYTHING_SORT_DATE_RUN_DESCENDING = 26;

	private const int EVERYTHING_TARGET_MACHINE_X86 = 1;
	private const int EVERYTHING_TARGET_MACHINE_X64 = 2;
	private const int EVERYTHING_TARGET_MACHINE_ARM = 3;
	
	private const string DllName = "Everything64.dll";

	[DllImport(DllName, CharSet = CharSet.Unicode)]
	public static extern uint Everything_SetSearchW(string lpSearchString);

	[DllImport(DllName)]
	public static extern void Everything_SetMatchPath(bool bEnable);

	[DllImport(DllName)]
	public static extern void Everything_SetMatchCase(bool bEnable);

	[DllImport(DllName)]
	public static extern void Everything_SetMatchWholeWord(bool bEnable);

	[DllImport(DllName)]
	public static extern void Everything_SetRegex(bool bEnable);

	[DllImport(DllName)]
	public static extern void Everything_SetMax(uint dwMax);

	[DllImport(DllName)]
	public static extern void Everything_SetOffset(uint dwOffset);

	[DllImport(DllName)]
	public static extern bool Everything_GetMatchPath();

	[DllImport(DllName)]
	public static extern bool Everything_GetMatchCase();

	[DllImport(DllName)]
	public static extern bool Everything_GetMatchWholeWord();

	[DllImport(DllName)]
	public static extern bool Everything_GetRegex();

	[DllImport(DllName)]
	public static extern uint Everything_GetMax();

	[DllImport(DllName)]
	public static extern uint Everything_GetOffset();

	[DllImport(DllName)]
	public static extern IntPtr Everything_GetSearchW();

	[DllImport(DllName)]
	public static extern uint Everything_GetLastError();

	[DllImport(DllName)]
	public static extern bool Everything_QueryW(bool bWait);

	[DllImport(DllName)]
	public static extern void Everything_SortResultsByPath();

	[DllImport(DllName)]
	public static extern uint Everything_GetNumFileResults();

	[DllImport(DllName)]
	public static extern uint Everything_GetNumFolderResults();

	[DllImport(DllName)]
	public static extern uint Everything_GetNumResults();

	[DllImport(DllName)]
	public static extern uint Everything_GetTotFileResults();

	[DllImport(DllName)]
	public static extern uint Everything_GetTotFolderResults();

	[DllImport(DllName)]
	public static extern uint Everything_GetTotResults();

	[DllImport(DllName)]
	public static extern bool Everything_IsVolumeResult(uint nIndex);

	[DllImport(DllName)]
	public static extern bool Everything_IsFolderResult(uint nIndex);

	[DllImport(DllName)]
	public static extern bool Everything_IsFileResult(uint nIndex);

	[DllImport(DllName, CharSet = CharSet.Unicode)]
	public static extern void Everything_GetResultFullPathName(uint nIndex, StringBuilder lpString, uint nMaxCount);

	[DllImport(DllName)]
	public static extern void Everything_Reset();

	[DllImport(DllName, CharSet = CharSet.Unicode)]
	public static extern IntPtr Everything_GetResultFileName(uint nIndex);

	// Everything 1.4
	[DllImport(DllName)]
	public static extern void Everything_SetSort(uint dwSortType);

	[DllImport(DllName)]
	public static extern uint Everything_GetSort();

	[DllImport(DllName)]
	public static extern uint Everything_GetResultListSort();

	[DllImport(DllName)]
	public static extern void Everything_SetRequestFlags(uint dwRequestFlags);

	[DllImport(DllName)]
	public static extern uint Everything_GetRequestFlags();

	[DllImport(DllName)]
	public static extern uint Everything_GetResultListRequestFlags();

	[DllImport(DllName, CharSet = CharSet.Unicode)]
	public static extern IntPtr Everything_GetResultExtension(uint nIndex);

	[DllImport(DllName)]
	public static extern bool Everything_GetResultSize(uint nIndex, out long lpFileSize);

	[DllImport(DllName)]
	public static extern bool Everything_GetResultDateCreated(uint nIndex, out long lpFileTime);

	[DllImport(DllName)]
	public static extern bool Everything_GetResultDateModified(uint nIndex, out long lpFileTime);

	[DllImport(DllName)]
	public static extern bool Everything_GetResultDateAccessed(uint nIndex, out long lpFileTime);

	[DllImport(DllName)]
	public static extern uint Everything_GetResultAttributes(uint nIndex);

	[DllImport(DllName, CharSet = CharSet.Unicode)]
	public static extern IntPtr Everything_GetResultFileListFileName(uint nIndex);

	[DllImport(DllName)]
	public static extern uint Everything_GetResultRunCount(uint nIndex);

	[DllImport(DllName)]
	public static extern bool Everything_GetResultDateRun(uint nIndex, out long lpFileTime);

	[DllImport(DllName)]
	public static extern bool Everything_GetResultDateRecentlyChanged(uint nIndex, out long lpFileTime);

	[DllImport(DllName, CharSet = CharSet.Unicode)]
	public static extern IntPtr Everything_GetResultHighlightedFileName(uint nIndex);

	[DllImport(DllName, CharSet = CharSet.Unicode)]
	public static extern IntPtr Everything_GetResultHighlightedPath(uint nIndex);

	[DllImport(DllName, CharSet = CharSet.Unicode)]
	public static extern IntPtr Everything_GetResultHighlightedFullPathAndFileName(uint nIndex);

	[DllImport(DllName)]
	public static extern uint Everything_GetRunCountFromFileName(string lpFileName);

	[DllImport(DllName)]
	public static extern bool Everything_SetRunCountFromFileName(string lpFileName, uint dwRunCount);

	[DllImport(DllName)]
	public static extern uint Everything_IncRunCountFromFileName(string lpFileName);
}