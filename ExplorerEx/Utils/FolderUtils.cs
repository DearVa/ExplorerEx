using System;
using System.Collections.Generic;

namespace ExplorerEx.Utils; 

internal class FolderUtils {
	public static HashSet<string> SpecialFolders { get; } = new();
	
	public void Initialize() {
		foreach (var specialFolder in Enum.GetValues<Environment.SpecialFolder>()) {
			SpecialFolders.Add(Environment.GetFolderPath(specialFolder));
		}
	}
}