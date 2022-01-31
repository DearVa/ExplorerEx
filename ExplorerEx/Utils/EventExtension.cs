using System;
using System.Reflection;
using System.Windows;
using System.Windows.Markup;

namespace ExplorerEx.Utils;

internal class EventExtension : MarkupExtension {
	[ConstructorArgument("path")]
	public string HandlerMethodName { get; set; }

	public EventExtension(string handlerMethodName) {
		HandlerMethodName = handlerMethodName;
	}

	public override object ProvideValue(IServiceProvider serviceProvider) {
		var target = (IProvideValueTarget)serviceProvider.GetService(typeof(IProvideValueTarget));
		var element = (FrameworkElement)target!.TargetObject;
		var dataContent = element.DataContext;
		while (dataContent == null) {
			var parent = element.Parent;
			if (parent is FrameworkElement fe) {
				element = fe;
				dataContent = element.DataContext;
			} else {
				dataContent = Window.GetWindow(element)!.DataContext;
				break;
			}
		}
		var handlerMethod = dataContent.GetType().GetMethod(HandlerMethodName);
		var eventInfo = (EventInfo)target.TargetProperty;
		return handlerMethod!.CreateDelegate(eventInfo.EventHandlerType!, dataContent);
	}
}