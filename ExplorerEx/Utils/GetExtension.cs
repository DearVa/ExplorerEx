using System;
using System.Windows;
using System.Windows.Markup;

namespace ExplorerEx.Utils; 

/// <summary>
/// 提供一种一次性获取属性的拓展
/// </summary>
public class GetExtension : MarkupExtension {
	[ConstructorArgument("path")]
	public string PropertyName { get; }

	public GetExtension(string propertyName) {
		PropertyName = propertyName;
	}

	public override object ProvideValue(IServiceProvider serviceProvider) {
		var dc = ((FrameworkElement)((IProvideValueTarget)serviceProvider.GetService(typeof(IProvideValueTarget)))!.TargetObject).DataContext;
		return dc!.GetType()!.GetProperty(PropertyName)!.GetValue(dc);
	}
}