using ExplorerEx.View.Controls;
using System;
using System.Collections.Generic;
using System.Windows;

namespace ExplorerEx.Win32;

public enum DataObjectType {
	Unknown, FileDrop, Bitmap, Text, UnicodeText, Html
}

/// <summary>
/// 解析一个DataObject的数据类型
/// </summary>
public class DataObjectContent {
	/// <summary>
	/// 当剪切板变化时触发，数据会放在<see cref="Clipboard"/>中
	/// </summary>
	public static event Action? ClipboardChanged;

	public static DataObjectContent Clipboard { get; private set; }

	/// <summary>
	/// 当外部拖放进来的时候，会解析并存放在这里
	/// </summary>
	public static DataObjectContent? Drag { get; private set; }

	public DataObjectType Type { get; }

	public object? Data { get; }

	private static readonly DataObjectContent Default;

	private static readonly Dictionary<string, DataObjectType> TypePairs = new() {
		{ DataFormats.FileDrop, DataObjectType.FileDrop },
        { DataFormats.Bitmap, DataObjectType.Bitmap },
        { DataFormats.Text, DataObjectType.Text },
        { DataFormats.UnicodeText, DataObjectType.UnicodeText },
        { DataFormats.Html, DataObjectType.Html }
	};

	static DataObjectContent() {
		Clipboard = Default = new DataObjectContent(null, DataObjectType.Unknown);
	}

	private DataObjectContent(object? data, DataObjectType type) {
		Data = data;
		Type = type;
	}

	/// <summary>
	/// 解析，如果是支持的格式
	/// </summary>
	/// <param name="iDataObject"></param>
	/// <returns></returns>
	public static DataObjectContent Parse(IDataObject? iDataObject) {
		if (iDataObject is DataObject dataObject) {
			foreach (var (key, type) in TypePairs) {
				try {
					var data = dataObject.GetData(key);
					if (data != null) {
						return new DataObjectContent(data, type); // 拿到第一种就停止
					}
				} catch {  // 有时候如果数据不受支持就会报异常，这是正确的情况，无视即可 https://stackoverflow.com/a/34092811/6116637
					return Default;
				}
			}
		}
		return Default;
	}

	public static void HandleClipboardChanged() {
		Clipboard = Parse(System.Windows.Clipboard.GetDataObject());
		ClipboardChanged?.Invoke();
	}

	public static void HandleDragEnter(DragEventArgs e) {
		if (DragFilesPreview.IsShown) {
			return;
		}
		Drag = Parse(e.Data);
		if (Drag.Type == DataObjectType.FileDrop && Drag.Data is string[] { Length: > 0 } filePaths) {
			DragFilesPreview.Singleton.SetFilePaths(filePaths);
			DragFilesPreview.ShowPreview();
		}
	}

	public static void HandleDragLeave() {
		if (DragFilesPreview.IsInternalDrag) {
			return;
		}
		Drag = null;
		DragFilesPreview.HidePreview();
	}
}