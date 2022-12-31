using System;

namespace ExplorerEx.Shell32; 

[Flags]
internal enum SHCNE : uint {
	RenameItem = 0x00000001,
	Create = 0x00000002,
	Delete = 0x00000004,
	Mkdir = 0x00000008,
	Rmdir = 0x00000010,
	MediaInserted = 0x00000020,
	MediaRemoved = 0x00000040,
	DriveRemoved = 0x00000080,
	DriveAdd = 0x00000100,
	NetShare = 0x00000200,
	NetUnshare = 0x00000400,
	Attributes = 0x00000800,
	UpdateDir = 0x00001000,
	UpdateItem = 0x00002000,
	ServerDisconnect = 0x00004000,
	UpdateImage = 0x00008000,
	DriveAddGUI = 0x00010000,
	RenameFolder = 0x00020000,
	FreeSpace = 0x00040000,
	ExtendedEvent = 0x04000000,
	AssocChanged = 0x08000000,
	DiskEvents = 0x0002381F,
	GlobalEvents = 0x0C0581E0,
	AllEvents = 0x7FFFFFFF,
	Interrupt = 0x80000000
}