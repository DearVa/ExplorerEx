using System.Windows;
using ExplorerEx.Win32;

namespace ExplorerEx; 

public partial class App {
	protected override void OnStartup(StartupEventArgs e) {
		base.OnStartup(e);
		IconHelper.InitializeDefaultIcons(Resources);
	}
}