using System.Windows;

namespace ExplorerEx.Utils; 

/// <summary>
/// 一些拓展方法
/// </summary>
internal static class Extensions {
	public static DragDropEffects GetActualEffect(this DragDropEffects effects) {
		if (effects.HasFlag(DragDropEffects.Move)) {
			return DragDropEffects.Move;
		}
		if (effects.HasFlag(DragDropEffects.Copy)) {
			return DragDropEffects.Copy;
		}
		if (effects.HasFlag(DragDropEffects.Link)) {
			return DragDropEffects.Link;
		}
		if (effects.HasFlag(DragDropEffects.Scroll)) {
			return DragDropEffects.Scroll;
		}
		return DragDropEffects.None;
	}
}