using System;
using System.Runtime.InteropServices;

namespace ExplorerEx.Shell32; 

/// <summary>
/// Shell文件操作数据类型
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public class ShFileOpStruct {
	public IntPtr hwnd;
	/// <summary>
	/// 设置操作方式
	/// </summary>
	public FileOpType fileOpType;
	/// <summary>
	/// 源文件路径
	/// </summary>
	public string pFrom;
	/// <summary>
	/// 目标文件路径
	/// </summary>
	public string pTo;
	/// <summary>
	/// 允许恢复
	/// </summary>
	public FileOpFlags fFlags;
	/// <summary>
	/// 监测有无中止
	/// </summary>
	public bool fAnyOperationsAborted;
	/// <summary>
	/// 基本没啥用
	/// </summary>
	public IntPtr hNameMappings;
	/// <summary>
	/// 设置标题
	/// </summary>
	public string lpszProgressTitle;
}

/// <summary>
/// 文件操作方式
/// </summary>
public enum FileOpType {
	/// <summary>
	/// 移动
	/// </summary>
	Move = 0x0001,
	/// <summary>
	/// 复制
	/// </summary>
	Copy = 0x0002,
	/// <summary>
	/// 删除
	/// </summary>
	Delete = 0x0003,
	/// <summary>
	/// 重命名
	/// </summary>
	Rename = 0x0004
}

/// <summary>
/// 参见：http://msdn.microsoft.com/zh-cn/library/bb759795(v=vs.85).aspx
/// </summary>
[Flags]
public enum FileOpFlags {
	/// <summary>
	/// pTo 指定了多个目标文件，而不是单个目录
	/// The pTo member specifies multiple destination files (one for each source file) rather than one directory where all source files are to be deposited.
	/// </summary>
	MultiDestFiles = 0x1,
	/// <summary>
	/// 不再使用
	/// </summary>
	[Obsolete]
	ConfirmMouse = 0x2,
	/// <summary>
	/// 不显示一个进度对话框
	/// Do not display a progress dialog box.
	/// </summary>
	Silent = 0x4,
	/// <summary>
	/// 碰到有抵触的名字时，自动分配前缀
	/// Give the file being operated on a new name in a move, copy, or rename operation if a file with the target name already exists.
	/// </summary>
	RenameOnCollision = 0x8,
	/// <summary>
	/// 不对用户显示提示
	/// Respond with "Yes to All" for any dialog box that is displayed.
	/// </summary>
	NoConformation = 0x10,
	/// <summary>
	/// 填充 hNameMappings 字段，必须使用 SHFreeNameMappings 释放
	/// If FOF_RENAMEONCOLLISION is specified and any files were renamed, assign a name mapping object containing their old and new names to the hNameMappings member.
	/// </summary>
	WantMappingHandle = 0x20,
	/// <summary>
	/// 允许撤销
	/// Preserve Undo information, if possible. If pFrom does not contain fully qualified path and file names, this flag is ignored.
	/// </summary>
	AllowUndo = 0x40,
	/// <summary>
	/// 使用 *.* 时, 只对文件操作
	/// Perform the operation on files only if a wildcard file name (*.*) is specified.
	/// </summary>
	FilesOnly = 0x80,
	/// <summary>
	/// 简单进度条，意味着不显示文件名。
	/// Display a progress dialog box but do not show the file names.
	/// </summary>
	SimpleProgress = 0x100,
	/// <summary>
	/// 建新目录时不需要用户确定
	/// Do not confirm the creation of a new directory if the operation requires one to be created.
	/// </summary>
	NoConfirmMkdir = 0x200,
	/// <summary>
	/// 不显示出错用户界面
	/// Do not display a user interface if an error occurs.
	/// </summary>
	NoErrorUI = 0x400,
	/// <summary>
	///  不复制 NT 文件的安全属性
	/// Do not copy the security attributes of the file.
	/// </summary>
	NoCopySecurityAttributes = 0x800,
	/// <summary>
	///  不递归目录
	/// Only operate in the local directory. Don't operate recursively into subdirectories.
	/// </summary>
	NoRecursion = 0x1000,
	/// <summary>
	/// Do not move connected files as a group. Only move the specified files.
	/// </summary>
	NoConnectedElements = 0x2000,
	/// <summary>
	/// Send a warning if a file is being destroyed during a delete operation rather than recycled. This flag partially overrides FOF_NOCONFIRMATION.
	/// </summary>
	WantNukeWarning = 0x4000,
	/// <summary>
	/// Treat reparse points as objects, not containers.
	/// </summary>
	NoRecurseReparse = 0x8000,
}