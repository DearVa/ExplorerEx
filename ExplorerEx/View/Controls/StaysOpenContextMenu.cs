using System.Windows.Controls;
using System.Windows;

namespace ExplorerEx.View.Controls; 

/// <summary>
/// debug用
/// </summary>
public class StaysOpenContextMenu : ContextMenu {
	private bool mustStayOpen;

	static StaysOpenContextMenu() {
		IsOpenProperty.OverrideMetadata(typeof(StaysOpenContextMenu), new FrameworkPropertyMetadata(false, null, CoerceIsOpen));
		StaysOpenProperty.OverrideMetadata(typeof(StaysOpenContextMenu), new FrameworkPropertyMetadata(false, PropertyChanged, CoerceStaysOpen));
	}

	private static void PropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		((StaysOpenContextMenu)d).mustStayOpen = (bool)e.NewValue;
	}

	private static object CoerceStaysOpen(DependencyObject d, object value) {
		d.CoerceValue(IsOpenProperty);
		return value;
	}

	private static object CoerceIsOpen(DependencyObject d, object value) {
		var menu = (StaysOpenContextMenu)d;
		if (menu.StaysOpen && menu.mustStayOpen) {
			return true;
		}

		return value;
	}

	public void CloseContextMenu() {
		mustStayOpen = false;
		IsOpen = false;
	}
}