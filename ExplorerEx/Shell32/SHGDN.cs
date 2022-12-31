using System;

namespace ExplorerEx.Shell32;

[Flags]
public enum SHGDN {
	Normal = 0x0000,
	InFolder = 0x0001,
	ForEditing = 0x1000,
	ForAddressBar = 0x4000,
	ForParsing = 0x8000,
}