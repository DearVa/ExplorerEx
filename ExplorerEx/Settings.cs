using System.Windows;
using ExplorerEx.Utils;
using Microsoft.Win32;

namespace ExplorerEx;

/// <summary>
/// 全局设置，严格单例模式
/// </summary>
public class Settings : DependencyObject {
	private static readonly RegistryKey Reg = Registry.CurrentUser.OpenSubKey(@"Software\Dear.Va\ExplorerEx\Settings", true) ?? Registry.CurrentUser.CreateSubKey(@"Software\Dear.Va\ExplorerEx\Settings", true);

	public static Settings Instance { get; } = new();

	/// <summary>
	/// 文本编辑器
	/// </summary>
	public static readonly DependencyProperty TextEditorProperty = DependencyProperty.Register(
		"TextEditor", typeof(string), typeof(Settings), new PropertyMetadata(Default("TextEditor", @"notepad.exe"), SettingsChanged));

	public string TextEditor {
		get => (string)GetValue(TextEditorProperty);
		set => SetValue(TextEditorProperty, value);
	}

	private static object Default(string settingsName, object defaultValue = null) {
		return Reg.GetValue(settingsName) ?? defaultValue;
	}

	private static void SettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		Reg.SetValue(e.Property.Name, e.NewValue);
	}

	static Settings() { }

	private Settings() { }
}