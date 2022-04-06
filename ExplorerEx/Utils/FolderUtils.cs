using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ExplorerEx.Utils; 

internal static class FolderUtils {
	public static HashSet<string> SpecialFolders { get; } = new();
	
	public static void Initialize() {
		foreach (var specialFolder in Enum.GetValues<Environment.SpecialFolder>()) {
			SpecialFolders.Add(Environment.GetFolderPath(specialFolder));
		}
	}

	/// <summary>
	/// 用<see cref="folderName"/>加随机后缀，生成在Temp文件夹
	/// </summary>
	/// <param name="folderName"></param>
	/// <returns></returns>
	public static string GetRandomFolderInTemp(string folderName) {
		var tempPath = Path.GetTempPath();
		string randomPath;
		do {
			randomPath = Path.Combine(tempPath, folderName + '_' + Guid.NewGuid().ToString()[..6]);
		} while (Directory.Exists(randomPath));
		Directory.CreateDirectory(randomPath);
		return randomPath;
	}

	[DllImport("shlwapi.dll", EntryPoint = "PathIsDirectoryEmpty", CharSet = CharSet.Unicode)]
	public static extern bool IsEmptyFolder(string path);
}