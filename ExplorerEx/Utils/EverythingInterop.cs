using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ExplorerEx.Utils;

public class EverythingException : Exception {
	public EverythingException(string msg) : base(msg) { }
}

public static class EverythingInterop {
	public enum ResultType {
		Ok = 0,
		ErrorMemory = 1,
		ErrorIpc = 2,
		ErrorRegistryClassException = 3,
		ErrorCreateWindow = 4,
		ErrorCreateThread = 5,
		ErrorInvalidIndex = 6,
		ErrorInvalidCall = 7
	}

	[Flags]
	public enum RequestType {
		FileName = 0x00000001,
		Path = 0x00000002,
		FullPathAndFileName = 0x00000004,
		Extension = 0x00000008,
		Size = 0x00000010,
		DateCreated = 0x00000020,
		DateModified = 0x00000040,
		DateAccessed = 0x00000080,
		Attributes = 0x00000100,
		FileListFileName = 0x00000200,
		RunCount = 0x00000400,
		DateRun = 0x00000800,
		DateRecentlyChanged = 0x00001000,
		HighlightedFileName = 0x00002000,
		HighlightedPath = 0x00004000,
		HighlightedFullPathAndFileName = 0x00008000
	}

	public enum SortMode {
		NameAscending = 1,
		NameDescending = 2,
		PathAscending = 3,
		PathDescending = 4,
		SizeAscending = 5,
		SizeDescending = 6,
		ExtensionAscending = 7,
		ExtensionDescending = 8,
		TypeNameAscending = 9,
		TypeNameDescending = 10,
		DateCreatedAscending = 11,
		DateCreatedDescending = 12,
		DateModifiedAscending = 13,
		DateModifiedDescending = 14,
		AttributesAscending = 15,
		AttributesDescending = 16,
		FileListFileNameAscending = 17,
		FileListFileNameDescending = 18,
		RunCountAscending = 19,
		RunCountDescending = 20,
		DateRecentlyChangedAscending = 21,
		DateRecentlyChangedDescending = 22,
		DateAccessedAscending = 23,
		DateAccessedDescending = 24,
		DateRunAscending = 25,
		DateRunDescending = 26
	}

	public enum TargetMachineType {
		X86 = 1,
		X64 = 2,
		Arm = 3
	}

	private const string DllName = "Everything64.dll";

	private static void Check(uint result) {
		if (result != 0) {
			throw new EverythingException(Enum.GetName(typeof(ResultType), result) ?? "Unknown Error");
		}
	}

	[DllImport(DllName, CharSet = CharSet.Unicode)]
	private static extern uint Everything_SetSearchW(string lpSearchString);

	[DllImport(DllName)]
    private static extern IntPtr Everything_GetSearchW();

    public static string Search {
	    get => Marshal.PtrToStringUni(Everything_GetSearchW());
		set => Check(Everything_SetSearchW(value));
	}

    [DllImport(DllName)]
	private static extern void Everything_SetMatchPath(bool isEnable);

	[DllImport(DllName)]
	private static extern bool Everything_GetMatchPath();

	public static bool IsMatchPath {
		get => Everything_GetMatchPath();
		set => Everything_SetMatchPath(value);
	}

	[DllImport(DllName)]
	private static extern void Everything_SetMatchCase(bool isEnable);

	[DllImport(DllName)]
	private static extern bool Everything_GetMatchCase();

    public static bool IsMatchCase {
        get => Everything_GetMatchCase();
        set => Everything_SetMatchCase(value);
    }

	[DllImport(DllName)]
	private static extern void Everything_SetMatchWholeWord(bool isEnable);

	[DllImport(DllName)]
	private static extern bool Everything_GetMatchWholeWord();

    public static bool IsMatchWholeWord {
        get => Everything_GetMatchWholeWord();
        set => Everything_SetMatchWholeWord(value);
    }

    [DllImport(DllName)]
    private static extern void Everything_SetRegex(bool isEnable);

	[DllImport(DllName)]
	private static extern bool Everything_GetRegex();

    public static bool UseRegex {
        get => Everything_GetRegex();
        set => Everything_SetRegex(value);
    }

	[DllImport(DllName)]
	private static extern void Everything_SetMax(uint max);

	[DllImport(DllName)]
	private static extern uint Everything_GetMax();

    public static uint Max {
        get => Everything_GetMax();
        set => Everything_SetMax(value);
    }

	[DllImport(DllName)]
	private static extern void Everything_SetOffset(uint offset);

	[DllImport(DllName)]
	private static extern uint Everything_GetOffset();
	
    public static uint Offset {
        get => Everything_GetOffset();
        set => Everything_SetOffset(value);
    }

    [DllImport(DllName)]
    private static extern uint Everything_GetLastError();

    public static ResultType GetLastError() {
		return (ResultType)Everything_GetLastError();
    }

	[DllImport(DllName, EntryPoint = "Everything_QueryW")]
	public static extern bool Query(bool waitForSearch);

	[DllImport(DllName, EntryPoint = "Everything_SortResultsByPath")]
	public static extern void SortResultsByPath();

	[DllImport(DllName, EntryPoint = "Everything_GetNumFileResults")]
	public static extern uint GetNumFileResults();

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

	[DllImport(DllName, EntryPoint = "Everything_Reset")]
	public static extern void Reset();

	[DllImport(DllName, CharSet = CharSet.Unicode)]
	public static extern IntPtr Everything_GetResultFileName(uint nIndex);

	// Everything 1.4
	[DllImport(DllName, EntryPoint = "Everything_SetSort")]
	public static extern void SetSort(SortMode sortModeFlags);

	[DllImport(DllName, EntryPoint = "Everything_SetSort")]
	public static extern uint GetSort();

	[DllImport(DllName)]
	public static extern uint Everything_GetResultListSort();

	[DllImport(DllName, EntryPoint = "Everything_SetRequestFlags")]
	public static extern void SetRequestFlags(RequestType dwRequestTypeFlags);

	[DllImport(DllName, EntryPoint = "Everything_GetRequestFlags")]
	public static extern uint GetRequestFlags();

	[DllImport(DllName)]
	public static extern uint Everything_GetResultListRequestFlags();

	[DllImport(DllName, CharSet = CharSet.Unicode)]
	public static extern IntPtr Everything_GetResultExtension(uint nIndex);

	[DllImport(DllName)]
	public static extern bool GetResultSize(uint nIndex, out long lpFileSize);

	[DllImport(DllName)]
	public static extern bool Everything_GetResultDateCreated(uint nIndex, out long lpFileTime);

	[DllImport(DllName)]
	public static extern bool GetResultDateModified(uint nIndex, out long lpFileTime);

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

	[DllImport(DllName, CharSet = CharSet.Unicode)]
	private static extern uint Everything_GetResultFullPathName(uint nIndex, StringBuilder lpString, uint nMaxCount);

	private static readonly StringBuilder Buffer = new(260);

	public static string GetResultFullPathName(uint index) {
		lock (Buffer) {
			if (Everything_GetResultFullPathName(index, Buffer, 260) == 0) {
				throw new EverythingException("Everything_GetResultFullPathName");
			}
			return Buffer.ToString();
		}
	}

	[DllImport(DllName)]
	public static extern uint Everything_GetRunCountFromFileName(string lpFileName);

	[DllImport(DllName)]
	public static extern bool Everything_SetRunCountFromFileName(string lpFileName, uint dwRunCount);

	[DllImport(DllName)]
	public static extern uint Everything_IncRunCountFromFileName(string lpFileName);

	[DllImport(DllName)]
	public static extern int Everything_GetMajorVersion();

	[DllImport(DllName, EntryPoint = "Everything_IsQueryReply")]
	public static extern bool IsQueryReply(int msg, IntPtr wParam, IntPtr lParam, uint id);

	[DllImport(DllName, EntryPoint = "Everything_SetReplyID")]
	public static extern void SetReplyID(uint id);

	[DllImport(DllName, EntryPoint = "Everything_SetReplyWindow")]
	public static extern void SetReplyWindow(IntPtr hwnd);

	public static bool IsAvailable => Everything_GetMajorVersion() != 0;

	[StructLayout(LayoutKind.Sequential)]
	public struct CopyDataStruct {
		public IntPtr dwData;
		public uint cbData;
		public IntPtr lpData;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct EverythingIpcListw {
		public uint totalFolders;
		public uint totalFiles;
		public uint totalItems;
		public uint foldersCount;
		public uint filesCount;
		public uint itemsCount;
		public uint offset;
	}

	public class QueryReply {
		public uint TotalFolders { get; init; }
		public uint TotalFiles { get; init; }
		public uint TotalItems { get; init; }
		public uint FoldersCount { get; init; }
		public uint FilesCount { get; init; }
		public uint ItemsCount { get; init; }
		public string[] FullPaths { get; init; }
	}

	/// <summary>
	/// 直接用指针，爽爽子
	/// </summary>
	/// <param name="lpData"></param>
	/// <param name="cbData"></param>
	/// <returns></returns>
	public static unsafe QueryReply ParseEverythingIpcResult(IntPtr lpData, uint cbData) {
		var eil = Marshal.PtrToStructure<EverythingIpcListw>(lpData);
		var result = new string[eil.itemsCount];
		var ptr = (byte*)lpData.ToPointer();
		var uPtr = (uint*)ptr;
		var buf = new char[260];
		fixed (char* pBuf = buf) {
			for (var i = 0; i < eil.itemsCount; i++) {
				var filenameOffset = uPtr[8 + i * 3];
				var pathOffset = uPtr[9 + i * 3];
				var k = 0;
				for (var j = pathOffset; j < cbData; j += 2) {
					pBuf[k] = *(char*)(ptr + j);
					if (pBuf[k] == '\0') {
						pBuf[k++] = '\\';
						break;
					}
					k++;
				}
				for (var j = filenameOffset; j < cbData; j += 2) {
					pBuf[k] = *(char*)(ptr + j);
					if (pBuf[k] == '\0') {
						break;
					}
					k++;
				}
				result[i] = new string(pBuf, 0, k);
			}
		}
		return new QueryReply {
			TotalFolders = eil.totalFolders,
			TotalFiles = eil.totalFiles,
			TotalItems = eil.totalItems,
			FoldersCount = eil.foldersCount,
			FilesCount = eil.filesCount,
			ItemsCount = eil.itemsCount,
			FullPaths = result
		};
	}
}