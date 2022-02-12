using System.Windows;

namespace ExplorerEx.Win32;

public enum DataObjectType {
	Unknown, FileDrop, Bitmap, Text, UnicodeText, Html
}

/// <summary>
/// 解析一个DataObject的数据类型
/// </summary>
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
						Type = DataObjectType.FileDrop;
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