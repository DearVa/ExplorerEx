using System;

namespace ExplorerEx.Shell32;

[Flags]
public enum SHCONT {
	Folders = 0x0020,
	NonFolders = 0x0040,
	IncludeHidden = 0x0080,
	InitOnFirstNext = 0x0100,
	NetPrinterSrch = 0x0200,
	Shareable = 0x0400,
	Storage = 0x0800
}