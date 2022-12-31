using System;

namespace ExplorerEx.Shell32;

[Flags]
public enum SFGAO : uint {
	CanCopy = 0x00000001,
	CanMove = 0x00000002,
	CanLink = 0x00000004,
	Link = 0x00010000,
	Share = 0x00020000,
	Readonly = 0x00040000,
	Hidden = 0x00080000,
	Folder = 0x20000000,
	FileSystem = 0x40000000,
	HasSubfolder = 0x80000000,
}