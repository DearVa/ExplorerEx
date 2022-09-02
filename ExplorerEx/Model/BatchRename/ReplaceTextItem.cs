using System;
using System.Text.RegularExpressions;
using System.Windows;
using ExplorerEx.Command;

namespace ExplorerEx.Model.BatchRename;

/// <summary>
/// 替换文本，左边替换为右边，支持正则
/// </summary>
internal class ReplaceTextItem : DependencyObject {
	public static readonly DependencyProperty LeftProperty = DependencyProperty.Register(
		nameof(Left), typeof(string), typeof(ReplaceTextItem), new PropertyMetadata(string.Empty, PropertyChangedCallback));

	public string Left {
		get => (string)GetValue(LeftProperty);
		set => SetValue(LeftProperty, value);
	}

	public static readonly DependencyProperty RightProperty = DependencyProperty.Register(
		nameof(Right), typeof(string), typeof(ReplaceTextItem), new PropertyMetadata(string.Empty, PropertyChangedCallback));

	public string Right {
		get => (string)GetValue(RightProperty);
		set => SetValue(RightProperty, value);
	}

	private static void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		((ReplaceTextItem)d).Changed?.Invoke();
	}

	public string Replace(string originalFullPath) {
		if (Left == string.Empty) {
			return originalFullPath;
		}
		return Regex.Replace(originalFullPath, Left, Right);
	}

	public event Action? Changed;
}