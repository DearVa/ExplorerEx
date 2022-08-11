using System;
using System.Windows.Markup;

namespace HandyControl.Tools.Extension; 

public class NotNullExtension : MarkupExtension {
	public override object ProvideValue(IServiceProvider serviceProvider) {
		return ((IProvideValueTarget)serviceProvider.GetService(typeof(IProvideValueTarget)))!.TargetProperty != null;
	}
}