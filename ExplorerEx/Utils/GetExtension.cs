using System;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace ExplorerEx.Utils; 

/// <summary>
/// 提供一种一次性获取属性的拓展，效果类似于Binding的OneTime
/// </summary>
public class GetExtension : MarkupExtension {
	[ConstructorArgument("path")]
	public string PropertyName { get; }

	public GetExtension(string propertyName) {
		PropertyName = propertyName;
	}

	public override object ProvideValue(IServiceProvider serviceProvider) {
		var targetObj = (DependencyObject)((IProvideValueTarget)serviceProvider.GetService(typeof(IProvideValueTarget)))!.TargetObject;
		object dc = null;
		do {
			switch (targetObj) {
			case null:
			case Window:
				return null;
			case FrameworkElement fe:
				dc = fe.DataContext;
				break;
			}
			targetObj = VisualTreeHelper.GetParent(targetObj);
		} while (dc == null);
		return dc!.GetType()!.GetProperty(PropertyName)!.GetValue(dc);
	}
}