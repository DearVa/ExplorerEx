using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using ExplorerEx.Utils;
using ExplorerEx.View;
using ExplorerEx.View.Controls;
using ExplorerEx.Win32;
using HandyControl.Data;

namespace ExplorerEx.Model; 

/// <summary>
/// 所有可以显示在<see cref="FileGrid"/>中的项目的基类
/// </summary>
public abstract class FileViewBaseItem : SimpleNotifyPropertyChanged {
	[NotMapped]
	public ImageSource Icon {
		get => icon;
		protected set {
			if (icon != value) {
				icon = value;
				PropertyUpdateUI();
			}
		}
	}

	private ImageSource icon;

	public abstract string FullPath { get; protected set; }

	public string Name { get; set; }

	/// <summary>
	/// 类型描述
	/// </summary>
	public abstract string Type { get; }

	[NotMapped]
	public long FileSize { get; protected init; }

	[NotMapped]
	public bool IsFolder { get; protected set; }

	public bool IsBookmarked => BookmarkDbContext.Instance.BookmarkDbSet.Any(b => b.FullPath == FullPath);

	[NotMapped]
	public bool IsSelected {
		get => isSelected;
		set {
			if (isSelected != value) {
				isSelected = value;
				PropertyUpdateUI();
			}
		}
	}

	private bool isSelected;
	
	public SimpleCommand OpenCommand { get; }

	public SimpleCommand OpenInNewTabCommand { get; }

	public SimpleCommand OpenInNewWindowCommand { get; }

	/// <summary>
	/// 添加到书签
	/// </summary>
	public SimpleCommand AddToBookmarksCommand { get; }

	public SimpleCommand RemoveFromBookmarksCommand { get; }

	public SimpleCommand ShowPropertiesCommand { get; }

	protected FileViewBaseItem() {
		LostFocusCommand = new SimpleCommand(OnLostFocus);
		// ReSharper disable once AsyncVoidLambda
		OpenCommand = new SimpleCommand(async e => await OpenAsync((string)e != string.Empty));
		// ReSharper disable once AsyncVoidLambda
		OpenInNewTabCommand = new SimpleCommand(async _ => {
			if (IsFolder) {
				await FileTabControl.FocusedTabControl.OpenPathInNewTabAsync(FullPath);
			}
		});
		OpenInNewWindowCommand = new SimpleCommand(_ => new MainWindow(FullPath).Show());

		AddToBookmarksCommand = new SimpleCommand(_ => FileTabControl.FocusedTabControl.MainWindow.AddToBookmark(FullPath));
		RemoveFromBookmarksCommand = new SimpleCommand(_ => MainWindow.RemoveFromBookmark(this));

		ShowPropertiesCommand = new SimpleCommand(_ => Win32Interop.ShowFileProperties(FullPath));
	}

	/// <summary>
	/// LoadIcon用到了shell，那并不是一个可以多线程的方法，所以与其每次都Task.Run，不如提高粗粒度
	/// </summary>
	public abstract void LoadIcon();

	/// <summary>
	/// 打开该文件或者文件夹
	/// </summary>
	/// <param name="runAs">以管理员身份运行，只对文件有效</param>
	/// <returns></returns>
	public async Task OpenAsync(bool runAs = false) {
		if (IsFolder) {
			await FileTabControl.FocusedTabControl.SelectedTab.LoadDirectoryAsync(FullPath);
		} else {
			try {
				var psi = new ProcessStartInfo {
					FileName = FullPath,
					UseShellExecute = true
				};
				if (runAs) {
					psi.Verb = "runas";
				}
				Process.Start(psi);
			} catch (Exception e) {
				HandyControl.Controls.MessageBox.Error(e.Message, "Fail to open file".L());
			}
		}
	}

	#region 文件重命名
	[NotMapped]
	public string EditingName { get; set; }

	/// <summary>
	/// 校验文件名是否有效
	/// </summary>
	public Func<string, OperationResult<bool>> VerifyFunc { get; } = fn => new OperationResult<bool> {
		Data = !FileUtils.IsProhibitedFileName(fn)
	};

	[NotMapped]
	public bool IsErrorFileName { get; set; }

	public SimpleCommand LostFocusCommand { get; }

	/// <summary>
	/// 显示重命名的输入框
	/// </summary>
	public void BeginRename() {
		EditingName = Name;
		PropertyUpdateUI(nameof(EditingName));
	}

	public void StopRename() {
		if (!IsErrorFileName && EditingName != Name) {
			if (Rename()) {
				Name = EditingName;
				PropertyUpdateUI(nameof(Name));
			}
		}
		EditingName = null;
		PropertyUpdateUI(nameof(EditingName));
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