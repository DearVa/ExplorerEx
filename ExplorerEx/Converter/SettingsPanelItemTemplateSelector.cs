using System.Windows;
using System.Windows.Controls;

namespace ExplorerEx.Converter;

internal class SettingsPanelItemTemplateSelector : DataTemplateSelector {
	public DataTemplate SettingsSelectItemTemplate { get; set; } = null!;

	public DataTemplate SettingsStringItemTemplate { get; set; } = null!;

	public DataTemplate SettingsBooleanItemTemplate { get; set; } = null!;

	public DataTemplate SettingsNumberItemTemplate { get; set; } = null!;

	public DataTemplate SettingsExpanderTemplate { get; set; } = null!;

	public override DataTemplate SelectTemplate(object item, DependencyObject container) {
		return item switch {
			SettingsSelectItem => SettingsSelectItemTemplate,
			SettingsStringItem => SettingsStringItemTemplate,
			SettingsBooleanItem => SettingsBooleanItemTemplate,
			SettingsNumberItem => SettingsNumberItemTemplate,
			_ => SettingsExpanderTemplate
		};
	}
}