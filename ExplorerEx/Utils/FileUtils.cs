using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using static ExplorerEx.Win32.Win32Interop;

namespace ExplorerEx.Utils;
internal class FileUtils {
	private static readonly HashSet<string> ProhibitedFileNames = new() {
		"con",
		"prn",
		"aux",
		"nul",
		"com1",
		"com2",
		"com3",
		"com4",
		"com5",
		"com6",
		"com7",
		"com8",
		"com9",
		"lpt1",
		"lpt2",
		"lpt3",
		"lpt4",
		"lpt5",
		"lpt6",
		"lpt7",
		"lpt8",
		"lpt9"
	};

	/// <summary>
	/// 判断是否是非法的文件名，传入的是文件的完整路径，所以不需要GetFileName
	/// </summary>
	/// <param name="filePath"></param>
	/// <returns></returns>
	public bool IsProhibitedFileName(string filePath) {
		if (filePath == null) {
			throw new ArgumentNullException(nameof(filePath));
		}
		var fileName = Path.GetFileName(filePath).ToLower();
		if (ProhibitedFileNames.Contains(fileName)) {
			return true;
		}
		return fileName.Any(c => c is '\\' or '/' or ':' or '*' or '?' or '"' or '<' or '>' or '|');
	}

	private static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

	/// <summary>
	/// 将bytes单位的数字格式化成更易读的形式
	/// </summary>
	/// <param name="sizeInBytes"></param>
	/// <returns></returns>
	public static string FormatByteSize(long sizeInBytes) {
		switch (sizeInBytes) {
		case < 0:
			return string.Empty;
		case 0:
			return "0 byte";
		default:
			var mag = (int)Math.Log(sizeInBytes, 1024);

			// 1L << (mag * 10) == 2 ^ (10 * mag) 
			// [i.e. the number of bytes in the unit corresponding to mag]
			var adjustedSize = (double)sizeInBytes / (1L << (mag * 10));

			// make adjustment when the value is large enough that
			// it would round up to 1000 or more
			if (Math.Round(adjustedSize, 1) >= 1000) {
				mag += 1;
				adjustedSize /= 1024;
			}

			return $"{adjustedSize:n1} {SizeSuffixes[mag]}";
		}
	}

	/// <summary>
	/// Shell文件操作
	/// </summary>
	/// <param name="type"></param>
	/// <param name="sourceFiles"></param>
	/// <param name="destinationFiles"></param>
	/// <exception cref="ArgumentException"></exception>
	/// <exception cref="IOException"></exception>
	public static void FileOperation(FileOpType type, IList<string> sourceFiles, IList<string> destinationFiles = null) {
		if (sourceFiles is not { Count: > 0 }) {
			return;
		}
		if (type != FileOpType.Delete && (destinationFiles == null || sourceFiles.Count != destinationFiles.Count)) {
			throw new ArgumentException("原文件与目标文件个数不匹配");
		}
		var fFlags = FILEOP_FLAGS.FOF_NOCONFIRMMKDIR | FILEOP_FLAGS.FOF_ALLOWUNDO;
		if (sourceFiles.Count > 1) {
			fFlags |= FILEOP_FLAGS.FOF_MULTIDESTFILES;
		}
		if (type == FileOpType.Delete) {
			fFlags |= FILEOP_FLAGS.FOF_NOCONFIRMATION;
		}
		var fo = new SHFILEOPSTRUCT {
			fileOpType = type,
			pFrom = ParseFileList(sourceFiles),
			fFlags = fFlags
		};
		if (type != FileOpType.Delete) {
			fo.pTo = ParseFileList(destinationFiles);
		}
		var result = SHFileOperation(fo);
		if (result != 0) {
			throw new IOException(GetErrorString(result));
		}
	}

	private static string ParseFileList(IEnumerable<string> files) {
		var sb = new StringBuilder();
		foreach (var file in files) {
			sb.Append(Path.GetFullPath(file)).Append('\0');
		}
		return sb.Append('\0').ToString();
	}

	/// <summary>
	/// ms-help://MS.MSDNQTR.v90.chs/shellcc/platform/shell/reference/functions/shfileoperation.htm
	/// </summary>
	/// <param name="code"></param>
	/// <returns></returns>
	private static string GetErrorString(int code) {
		return code switch {
			0 => "Success",
			0x74 => "The source is a root directory, which cannot be moved or renamed.",
			0x76 => "Security settings denied access to the source.",
			0x7C => "The path in the source or destination or both was invalid.",
			0x10000 => "An unspecified error occurred on the destination.",
			0x402 => "An unknown error occurred. This is typically due to an invalid path in the source or destination. This error does not occur on Windows Vista and later.",
			_ => $"Unknown Error: {code}"
		};
	}
}

class FileUtilsImpl : FileUtils { }