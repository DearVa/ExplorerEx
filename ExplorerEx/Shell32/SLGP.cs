using System;

namespace ExplorerEx.Shell32;

[Flags]
enum SLGP {
	/// <summary>Retrieves the standard short (8.3 format) file name</summary>
	ShortPath = 0x1,
	/// <summary>Retrieves the Universal Naming Convention (UNC) path name of the file</summary>
	UncPriority = 0x2,
	/// <summary>Retrieves the raw path name. A raw path is something that might not exist and may include environment variables that need to be expanded</summary>
	RawPath = 0x4
}