using System;
using System.Globalization;
using System.Windows.Data;

namespace ExplorerEx.Converter; 

internal class Handler2EventConverter : IValueConverter {
	public object Convert(object handler, Type handlerType, object dataContent, CultureInfo culture) {
		var handlerMethod = dataContent.GetType().GetMethod((string)handler);
		return handlerMethod!.CreateDelegate(handlerType, dataContent);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new NotImplementedException();
	}
}