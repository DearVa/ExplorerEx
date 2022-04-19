using System;
using System.Globalization;
using System.Windows.Data;
using ExplorerEx.Utils;

namespace ExplorerEx.Converter.Grouping; 

internal class FileSizeGroupingConverter : IValueConverter {
	public static Lazy<FileSizeGroupingConverter> Instance { get; } = new(new FileSizeGroupingConverter());

	private FileSizeGroupingConverter() { }

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		var size = (long)value!;
		return size switch {
			< 0L => "Unknown".L(),
			0L => "Empty".L() + " (0 KB)",
			<= 16L * 1024 => "Tiny".L() + " (0 - 16 KB)",
			<= 1024L * 1024 => "Small".L() + " (16 KB - 1 MB)",
			<= 128L * 1024 * 1024 => "Medium".L() + " (1 - 128 MB)",
			<= 1024L * 1024 * 1024 => "Large".L() + " (128 MB - 1 GB)",
			<= 4L * 1024 * 1024 * 1024 => "Huge".L() + " (1 - 4 GB)",
			_ => "Gigantic".L() + " (>4 GB)"
		};

		//return offset.TotalHours switch {
		//	< 24d when now.Day == dateTime.Day => new OrderedCollectionViewGroup("Today".L(), 0),
		//	< 24d => new OrderedCollectionViewGroup("Yesterday".L(), 1),
		//	< 7 * 24d => new OrderedCollectionViewGroup("ThisWeek".L(), 2),
		//	< 30 * 24d => new OrderedCollectionViewGroup("EarlierThisMonth".L(), 3),
		//	< 60 * 24d => new OrderedCollectionViewGroup("LastMonth".L(), 4),
		//	_ => new OrderedCollectionViewGroup("ALongTimeAgo".L(), 5)
		//};
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new NotImplementedException();
	}
}