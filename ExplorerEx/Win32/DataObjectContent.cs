using System.Windows;

namespace ExplorerEx.Win32;

public enum DataObjectType {
	Unknown, File, Bitmap, Text, UnicodeText, Html
}

public class DataObjectContent {
	public DataObjectType Type { get; }

	public DataObject Data { get; }

	public DataObjectContent(IDataObject dataObject) {
		if (dataObject is DataObject data) {
			Data = data;
			var formats = data.GetFormats();
			if (formats != null) {
				foreach (var format in formats) {
					switch (format) {
					case "FileDrop":
						Type = DataObjectType.File;
						return;  // 拿到一种就返回
					case "Bitmap":
						Type = DataObjectType.Bitmap;
						return;
					case "Text":
						Type = DataObjectType.Text;
						return;
					case "UnicodeText":
						Type = DataObjectType.UnicodeText;
						return;
					case "Html":
						Type = DataObjectType.Html;
						return;
					}
				}
			}
		}
	}
}