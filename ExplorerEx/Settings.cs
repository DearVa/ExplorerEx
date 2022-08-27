using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using ExplorerEx.Utils;
using Microsoft.Win32;

namespace ExplorerEx;

internal class SettingsCategory {
	public SettingsCategory(string header) {
		Header = header;
	}

	public string Header { get; }

	public List<SettingsCategoryItem> Items { get; } = new();
}

internal abstract class SettingsCategoryItem {
	protected SettingsCategoryItem(string header, string? description, string? icon) {
		Header = header;
		Description = description;
		if (icon != null) {
			Icon = Application.Current.Resources[icon] as ImageSource;
		}
	}

	public string Header { get; }

	public string? Description { get; }

	public ImageSource? Icon { get; }
}

internal class SettingsExpander : SettingsCategoryItem {
	public SettingsExpander(string header, string? description, string? icon) : base(header, description, icon) { }

	public List<SettingsItem> Items { get; } = new();
}

internal enum SettingsType {
	Unknown,
	Boolean,
	Integer,
	String,
	Select,
}

internal abstract class SettingsItem : SettingsCategoryItem, INotifyPropertyChanged {
	private class EmptySettingsItem : SettingsItem {
		public EmptySettingsItem() : base(null!, null!, null, null, SettingsType.Unknown) { }
	}

	internal static SettingsItem Empty { get; } = new EmptySettingsItem();

	protected SettingsItem(string fullName, string header, string? description, string? icon, SettingsType type) : base(header, description, icon) {
		FullName = fullName;
		Type = type;
		Self = this;
	}

	public string FullName { get; }

	public SettingsType Type { get; }

	public object? Value {
		get => value;
		set {
			if (this.value != value) {
				this.value = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(Self));
				if (value != null) {
					ConfigHelper.Save(FullName, value);
				} else {
					ConfigHelper.Delete(FullName);
				}
				Changed?.Invoke(value);
			}
		}
	}

	private object? value;

	/// <summary>
	/// 用于Binding
	/// </summary>
	public SettingsItem Self { get; }

	public object? Default { get; set; }

	public bool GetBoolean() => Convert.ToBoolean(Value);

	public int GetInt32() => Convert.ToInt32(Value);

	public string GetString() => Convert.ToString(Value) ?? Convert.ToString(Default) ?? string.Empty;

	/// <summary>
	/// 与<see cref="PropertyChanged"/>不同，这个是专门用于监测值的变化
	/// </summary>
	public event Action<object?>? Changed; 

	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}

internal class SettingsSelectItem : SettingsItem {
	public class Item {
		public Item(string header, int value) {
			Header = header.L();
			Value = value;
		}

		public string Header { get; }

		public int Value { get; }

		public override string ToString() {
			return Header;
		}
	}

	public SettingsSelectItem(string fullName, string header, string? description, string? icon) : base(fullName, header, description, icon, SettingsType.Select) { }

	public List<Item> Items { get; } = new();
}

internal class SettingsBooleanItem : SettingsItem {
	public SettingsBooleanItem(string fullName, string header, string? description, string? icon) : base(fullName, header, description, icon, SettingsType.Boolean) { }
}

internal class SettingsIntegerItem : SettingsItem {
	public SettingsIntegerItem(string fullName, string header, string? description, string? icon) : base(fullName, header, description, icon, SettingsType.Integer) { }
}

internal class SettingsStringItem : SettingsItem {
	public SettingsStringItem(string fullName, string header, string? description, string? icon) : base(fullName, header, description, icon, SettingsType.String) { }
}

public enum ColorMode {
	FollowSystem,
	Light,
	Dark
}

public enum WindowBackdrop {
	SolidColor,
	Acrylic,
	Mica
}

internal sealed class Settings {
	public static Settings Current { get; } = new();

	public ObservableCollection<SettingsCategory> Categories { get; } = new();

	private readonly Dictionary<string, SettingsItem> settings = new();

	#region 特殊/常用设置

	public static class CommonSettings {
		public const string ColorMode = "Appearance.Theme.ColorMode";
		public const string WindowBackdrop = "Appearance.Theme.WindowBackdrop";

		public const string DoubleClickGoUpperLevel = "Common.DoubleClickGoUpperLevel";

		public const string DontAskWhenRecycle = "Customize.DontAskWhenRecycle";
		public const string DontAskWhenDelete = "Customize.DontAskWhenDelete";

		public const string ShowHiddenFilesAndFolders = "Advanced.ShowHiddenFilesAndFolders";
		public const string ShowProtectedSystemFilesAndFolders = "Advanced.ShowProtectedSystemFilesAndFolders";
	}
	
	public static event Action? ThemeChanged;

	/// <summary>
	/// 是否为暗色模式，根据设置获取，如果是跟随系统，就获取系统色
	/// </summary>
	public bool IsDarkMode {
		get {
			var colorMode = (ColorMode)settings[CommonSettings.ColorMode].GetInt32();
			switch (colorMode) {
			case ColorMode.FollowSystem:
				try {
					using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
					return key?.GetValue("AppsUseLightTheme") is 0;
				} catch {
					return false;
				}
			case ColorMode.Light:
				return false;
			case ColorMode.Dark:
				return true;
			default:
				return false;
			}
		}
	}

	public WindowBackdrop WindowBackdrop => (WindowBackdrop)settings[CommonSettings.WindowBackdrop].GetInt32();

	private void RegisterEvents() {
		ThemeChanged = null;

		settings[CommonSettings.ColorMode].Changed += _ => ThemeChanged?.Invoke();
		settings[CommonSettings.WindowBackdrop].Changed += _ => ThemeChanged?.Invoke();
	}
	
	#endregion

	public SettingsItem this[string name] {
		get {
			if (settings.TryGetValue(name, out var value)) {
				return value;
			}
			return SettingsItem.Empty;
		}
	}

	public void LoadSettings() {
		using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ExplorerEx.Assets.Settings.xml")!;
		using var xml = XmlReader.Create(stream);
		if (!xml.Read() || xml.Name != "settings") {
			return;
		}

		SettingsCategory? category = null;
		SettingsExpander? expander = null;
		SettingsItem? item = null;
		while (xml.Read()) {
			string? header;
			switch (xml.Name) {
			case "category":
				if (xml.NodeType == XmlNodeType.EndElement && category != null) {
					Categories.Add(category);
					category = null;
				} else if ((header = xml.GetAttribute("header")) != null) {
					category = new SettingsCategory(header);
				}
				break;
			case "expander":
				if (xml.NodeType == XmlNodeType.EndElement) {
					expander = null;
				} else if (category != null && (header = xml.GetAttribute("header")) != null) {
					expander = new SettingsExpander(header, xml.GetAttribute("description"), xml.GetAttribute("icon"));
					category.Items.Add(expander);
				}
				break;
			case "item":
				if (xml.NodeType == XmlNodeType.EndElement) {
					item = null;
				} else if (category != null && (header = xml.GetAttribute("header")) != null) {
					if (!Enum.TryParse<SettingsType>(xml.GetAttribute("type"), true, out var type)) {
						continue;
					}
					string fullName;
					if (expander != null) {
						fullName = category.Header + '.' + expander.Header + '.' + header;
					} else {
						fullName = category.Header + '.' + header;
					}
					switch (type) {
					case SettingsType.Boolean:
						item = new SettingsBooleanItem(fullName, header, xml.GetAttribute("description"), xml.GetAttribute("icon"));
						break;
					case SettingsType.Integer:
						item = new SettingsIntegerItem(fullName, header, xml.GetAttribute("description"), xml.GetAttribute("icon"));
						break;
					case SettingsType.String:
						item = new SettingsStringItem(fullName, header, xml.GetAttribute("description"), xml.GetAttribute("icon"));
						break;
					case SettingsType.Select:
						item = new SettingsSelectItem(fullName, header, xml.GetAttribute("description"), xml.GetAttribute("icon"));
						break;
					default:
						continue;
					}
					item.Default = xml.GetAttribute("default");
					item.Value = ConfigHelper.Load(fullName) ?? xml.GetAttribute("default");
					if (expander != null) {
						expander.Items.Add(item);
					} else {
						category.Items.Add(item);
					}
					settings.Add(fullName, item);
				}
				break;
			case "option":
				if (xml.NodeType == XmlNodeType.Element && item is SettingsSelectItem ssi && (header = xml.GetAttribute("header")) != null) {
					ssi.Items.Add(new SettingsSelectItem.Item(header, ssi.Items.Count));
				}
				break;
			}
		}

		RegisterEvents();
	}
}