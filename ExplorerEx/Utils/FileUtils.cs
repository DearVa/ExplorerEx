using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.UI.StartScreen;

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
}