namespace ExplorerEx.Views.Controls;

public partial class SettingsPanel {
	public SettingsPanel() {
		InitializeComponent();
		ItemsControl.ItemsSource = Settings.Current.Categories;
	}
}