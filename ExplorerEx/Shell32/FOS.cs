using System;

namespace ExplorerEx.Shell32;

[Flags]
internal enum FOS {
	PickFolders = 0x00000020,
	ForceFileSystem = 0x00000040,
	NoValidate = 0x00000100,
	NoTestFileCreate = 0x00010000,
	DontAddToRecent = 0x02000000
}