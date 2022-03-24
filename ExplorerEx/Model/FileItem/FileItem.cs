using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ExplorerEx.Command;
using ExplorerEx.Utils;
using ExplorerEx.View.Controls;
using HandyControl.Data;

namespace ExplorerEx.Model; 

/// <summary>
/// 所有可以显示在<see cref="FileListView"/>中的项目的基类
/// </summary>
public abstract class FileItem : SimpleNotifyPropertyChanged {
	/// <summary>
	/// 图标，自动更新UI
	/// </summary>
	[NotMapped]
	public ImageSource Icon {
		get => icon;
		protected set {
			if (icon != value) {
				icon = value;
				UpdateUI();
			}
		}
	}

	private ImageSource icon;

	public abstract string FullPath { get; protected set; }

	public abstract string DisplayText { get; }

	public string Name { get; set; }

	/// <summary>
	/// 类型描述，自动更新UI
	/// </summary>
	[NotMapped]
	public string Type {
		get => type;
		protected set {
			if (type != value) {
				type = value;
				UpdateUI();
			}
		}
	}

	private string type;

	[NotMapped]
	public long FileSize {
		get => fileSize;
		protected set {
			if (fileSize != value) {
				fileSize = value;
				UpdateUI();
			}
		}
	}

	private long fileSize;

	[NotMapped]
	public bool IsFolder { get; protected set; }

	public bool IsBookmarked => BookmarkDbContext.Instance.BookmarkDbSet.Any(b => b.FullPath == FullPath);

	[NotMapped]
	public bool IsSelected {
		get => isSelected;
		set {
			if (isSelected != value) {
				isSelected = value;
				UpdateUI();
			}
		}
	}

	private bool isSelected;

	protected FileItem() {
		LostFocusCommand = new SimpleCommand(OnLostFocus);
	}

	/// <summary>
	/// 加载文件的各项属性
	/// </summary>
	public abstract void LoadAttributes();

	/// <summary>
	/// LoadIcon用到了shell，那并不是一个可以多线程的方法，所以与其每次都Task.Run，不如提高粗粒度
	/// </summary>
	public abstract void LoadIcon();

	public abstract void StartRename();

	#region 文件重命名

	/// <summary>
	/// 当前正在编辑中的名字，不为null就显示编辑的TextBox
	/// </summary>
	[NotMapped]
	public string EditingName {
		get => editingName;
		set {
			if (editingName != value) {
				editingName = value;
				UpdateUI();
			}
		}
	}

	private string editingName;

	/// <summary>
	/// 校验文件名是否有效
	/// </summary>
	public Func<string, OperationResult<bool>> VerifyFunc { get; } = fn => new OperationResult<bool> {
		Data = !FileUtils.IsProhibitedFileName(fn)
	};

	[NotMapped]
	public bool IsErrorFileName { get; set; }

	public SimpleCommand LostFocusCommand { get; }

	public void StopRename() {
		if (!IsErrorFileName && EditingName != Name) {
			if (Rename()) {
				Name = EditingName;
				UpdateUI(nameof(Name));
			}
		}
		EditingName = null;
	}

	public void OnLostFocus(object args) {
		StopRename();
	}

	/// <summary>
	/// 重命名，此时EditingName是新的名字
	/// </summary>
	protected abstract bool Rename();

	public override string ToString() {
		return FullPath;
	}

	#endregion
}

public class FileItemAttach {
	public static readonly DependencyProperty FileItemProperty = DependencyProperty.RegisterAttached(
		"FileItem", typeof(FileItem), typeof(FileItemAttach), new PropertyMetadata(default(FileItem)));

	public static void SetFileItem(DependencyObject element, FileItem value) {
		element.SetValue(FileItemProperty, value);
	}

	public static FileItem GetFileItem(DependencyObject element) {
		return (FileItem)element.GetValue(FileItemProperty);
	}
}
