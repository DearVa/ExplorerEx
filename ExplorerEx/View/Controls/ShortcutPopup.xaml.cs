using System;
using System.Windows;
using System.Windows.Input;
using ExplorerEx.Utils;

namespace ExplorerEx.View.Controls; 

/// <summary>
/// FileListView选中文件后的快捷菜单
/// </summary>
public partial class ShortcutPopup {
	public new double Opacity {
		set => RootBorder.Opacity = value;
	}

	public event Action? ShowMore;

	private readonly DelayAction moreButtonHoverDelayAction;

	public ShortcutPopup() {
		InitializeComponent();
		moreButtonHoverDelayAction = new DelayAction(TimeSpan.FromMilliseconds(500), () => ShowMore?.Invoke());
	}

	protected override void OnMouseEnter(MouseEventArgs e) {
		e.Handled = true;
		RootBorder.Opacity = 1;
		base.OnMouseEnter(e);
	}

	private void MoreButton_OnMouseEnter(object sender, MouseEventArgs e) {
		moreButtonHoverDelayAction.Start();
	}

	private void MoreButton_OnMouseLeave(object sender, MouseEventArgs e) {
		moreButtonHoverDelayAction.Stop();
	}

	private void MoreButton_OnClick(object sender, RoutedEventArgs e) {
		ShowMore?.Invoke();
	}
}