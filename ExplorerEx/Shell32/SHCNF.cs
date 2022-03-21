using System;

namespace ExplorerEx.Shell32; 

[Flags]
internal enum SHCNF {
	AcceptInterrupts = 0x1,
	AcceptNonInterrupts = 0x2,
}