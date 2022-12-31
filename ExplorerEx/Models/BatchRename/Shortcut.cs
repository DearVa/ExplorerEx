using System;
using ExplorerEx.Utils;

namespace ExplorerEx.Models.BatchRename; 

/// <summary>
/// 快捷操作，如小写转大写
/// </summary>
internal class Shortcut {
	/// <summary>
	/// 需要使用<see cref="LangConverter"/>转换
	/// </summary>
	public string Name { get; }

	public Func<string, string> Func { get; }

	public Shortcut(string name, Func<string, string> func) {
		Name = name;
		Func = func;
	}
}