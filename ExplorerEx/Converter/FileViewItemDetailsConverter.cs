using System;
using System.Globalization;
using System.Windows.Data;
using ExplorerEx.Models;
using ExplorerEx.Utils;
using HandyControl.Tools.Converter;

namespace ExplorerEx.Converter;

internal class SelectedItemsCount2TextConverter : IValueConverter {
	public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		if (value is int i and > 0) {
			return string.Format("Selected...Items".L(), i);
		}
		return null;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new InvalidOperationException();
	}
}

internal class ItemsCount2TextConverter : IValueConverter {
	public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		if (value is int i) {
			if (i > 1) {
				return string.Format("...Items".L(), i);
			}
			return string.Format("...Item".L(), i);
		}
		return null;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new InvalidOperationException();
	}
}

internal class FileListViewItemDetails0Converter : IValueConverter {
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		switch (value) {
		case DiskDriveItem ddi:
			if (ddi.Drive.IsReady) {
				return ddi.Drive.DriveFormat;
			}
			return string.Empty;
		case FileSystemItem fsi:
			return "DateModified".L() + ":".L() + fsi.DateModified;
		default:
			return string.Empty;
		}
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new InvalidOperationException();
	}
}

internal class FileListViewItemDetails1Converter : IValueConverter {
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		switch (value) {
		case DiskDriveItem ddi:
			if (ddi.Drive.IsReady) {
				return string.Format("...FreeOf...".L(), Long2FileSizeConverter.StaticConvert(ddi.FreeSpace), Long2FileSizeConverter.StaticConvert(ddi.TotalSpace));
			}
			return string.Empty;
		case FileListViewItem flv:
			return flv.FileSize == -1 ? string.Empty : "FileSize".L() + ":".L() + Long2FileSizeConverter.StaticConvert(flv.FileSize);
		default:
			return string.Empty;
		}
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new InvalidOperationException();
	}
}