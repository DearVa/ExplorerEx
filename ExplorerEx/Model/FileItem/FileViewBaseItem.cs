using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using ExplorerEx.Utils;
using ExplorerEx.View.Controls;
using HandyControl.Data;

namespace ExplorerEx.Model; 

/// <summary>
/// 所有可以显示在<see cref="FileGrid"/>中的项目的基类
/// </summary>
public abstract class FileViewBaseItem : SimpleNotifyPropertyChanged {
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

	protected FileViewBaseItem() {
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

	/// <summary>
	/// 打开该文件或者文件夹
	/// </summary>
	/// <param name="runAs">以管理员身份运行，只对可执行文件有效</param>
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
				if (runAs && this is FileSystemItem { IsExecutable: true }) {
					psi.Verb = "runas";
				}
				Process.Start(psi);
			} catch (Exception e) {
				HandyControl.Controls.MessageBox.Error(e.Message, "FailedToOpenFile".L());
			}
		}
	}

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