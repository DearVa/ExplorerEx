using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ExplorerEx.Utils; 

internal static class FolderUtils {
	public static HashSet<string> SpecialFolders { get; } = new();
	
	public static void Initialize() {
		foreach (var specialFolder in Enum.GetValues<Environment.SpecialFolder>()) {
			SpecialFolders.Add(Environment.GetFolderPath(specialFolder));
		}
	}

	[DllImport("shlwapi.dll", EntryPoint = "PathIsDirectoryEmpty", CharSet = CharSet.Unicode)]
	public static extern bool IsEmptyFolder(string path);
}