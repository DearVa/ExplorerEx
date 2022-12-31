using System;
using System.Globalization;
using System.Windows.Data;

namespace ExplorerEx.Converter.Grouping; 

internal class FileSizeGroupingConverter : IValueConverter {
	public static Lazy<FileSizeGroupingConverter> Instance { get; } = new(new FileSizeGroupingConverter());

	private FileSizeGroupingConverter() { }

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		if (value is not long size) {
			return Strings.Resources.Unknown;
		}

		return size switch {
			< 0L => Strings.Resources.Unknown,
			0L => Strings.Resources.Empty + " (0 KB)",
			<= 16L * 1024 => Strings.Resources.Tiny + " (0 - 16 KB)",
			<= 1024L * 1024 => Strings.Resources.Small + " (16 KB - 1 MB)",
			<= 128L * 1024 * 1024 => Strings.Resources.Medium + " (1 - 128 MB)",
			<= 1024L * 1024 * 1024 => Strings.Resources.Large + " (128 MB - 1 GB)",
			<= 4L * 1024 * 1024 * 1024 => Strings.Resources.Huge + " (1 - 4 GB)",
			_ => Strings.Resources.Gigantic + " (>4 GB)"
		};
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new NotImplementedException();
	}
}