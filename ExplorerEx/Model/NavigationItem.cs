using System.Collections.ObjectModel;
using System.Windows.Media;

namespace ExplorerEx.Model; 

/// <summary>
/// 导航窗格的Root项
/// </summary>
public class NavigationItem {
	public ImageSource Icon { get; private set; }

	public string Header { get; }

	public ObservableCollection<FileViewBaseItem> Items { get; } = new();
}