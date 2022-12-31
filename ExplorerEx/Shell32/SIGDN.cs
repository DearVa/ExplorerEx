namespace ExplorerEx.Shell32;

internal enum SIGDN : uint {
	NormalDisplay = 0,
	ParentRelativeParsing = 0x80018001,
	ParentRelativeForAddressBar = 0x8001c001,
	DesktopAbsoluteParsing = 0x80028000,
	ParentRelativeEditing = 0x80031001,
	DesktopAbsoluteEditing = 0x8004c000,
	FileSysPath = 0x80058000,
	Url = 0x80068000
}