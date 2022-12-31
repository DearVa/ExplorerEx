using System;
using System.Globalization;
using System.Windows.Data;

namespace ExplorerEx.Converter.Grouping;

internal class DateTimeGroupingConverter : IValueConverter {
	public static Lazy<DateTimeGroupingConverter> Instance { get; } = new(new DateTimeGroupingConverter());

	private DateTimeGroupingConverter() { }

	public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) {
		if (value is not DateTime dateTime) {
			return Strings.Resources.ALongTimeAgo;
		}
		var now = DateTime.Now;
		if (dateTime > now) {
			return Strings.Resources.Today;
		}
		if (dateTime.Year == now.Year) {
			if (dateTime.Month == now.Month) {
				return (now.Day - dateTime.Day) switch {
					0 => Strings.Resources.Today,
					1 => Strings.Resources.Yesterday,
					<= 7 => Strings.Resources.ThisWeek,
					<= 14 => Strings.Resources.LastWeek,
					_ => Strings.Resources.ThisMonth
				};
			}
			if (dateTime.Month == now.Month - 1) {
				return Strings.Resources.LastMonth;
			}
		}
		return Strings.Resources.ALongTimeAgo;

		//return offset.TotalHours switch {
		//	< 24d when now.Day == dateTime.Day => new OrderedCollectionViewGroup(Strings.Resources.Today, 0),
		//	< 24d => new OrderedCollectionViewGroup(Strings.Resources.Yesterday, 1),
		//	< 7 * 24d => new OrderedCollectionViewGroup(Strings.Resources.ThisWeek, 2),
		//	< 30 * 24d => new OrderedCollectionViewGroup(Strings.Resources.EarlierThisMonth, 3),
		//	< 60 * 24d => new OrderedCollectionViewGroup(Strings.Resources.LastMonth, 4),
		//	_ => new OrderedCollectionViewGroup(Strings.Resources.ALongTimeAgo, 5)
		//};
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new InvalidOperationException();
	}
}