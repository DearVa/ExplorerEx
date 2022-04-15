using System.Windows;
using System.Windows.Controls;
using ExplorerEx.Model;

namespace ExplorerEx.Converter; 

internal class FileAssocTemplateSelector : StyleSelector {
	public Style DefaultStyle { get; set; }

	public Style CustomStyle { get; set; }

	public override Style SelectStyle(object item, DependencyObject container) {
		return item is FileAssocItem ? CustomStyle : DefaultStyle;
	}
}