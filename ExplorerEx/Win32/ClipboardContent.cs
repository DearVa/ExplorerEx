using System.Windows;

namespace ExplorerEx.Win32;

public enum ClipboardDataType {
	Unknown, File, Bitmap, Text, UnicodeText, Html
}

public class ClipboardContent {
	public ClipboardDataType Type { get; }

	public DataObject Data { get; }

	public ClipboardContent(IDataObject dataObject) {
		if (dataObject is DataObject data) {
			Data = data;
			var formats = data.GetFormats();
			if (formats != null) {
				foreach (var format in formats) {
					switch (format) {
					case "FileDrop":
						Type = ClipboardDataType.File;
						return;  // 拿到一种就返回
					case "Bitmap":
						Type = ClipboardDataType.Bitmap;
						return;
					case "Text":
						Type = ClipboardDataType.Text;
						return;
					case "UnicodeText":
						Type = ClipboardDataType.UnicodeText;
						return;
					case "Html":
						Type = ClipboardDataType.Html;
						return;
					}
				}
			}
		}
	}
}