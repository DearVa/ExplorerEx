using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using ExplorerEx.Annotations;
using ExplorerEx.Utils;
using ExplorerEx.View;
using ExplorerEx.ViewModel;
using ExplorerEx.Win32;
using HandyControl.Data;

namespace ExplorerEx.Model; 

public abstract class FileViewBaseItem : INotifyPropertyChanged {
	protected FileViewGridViewModel OwnerViewModel { get; }

	public ImageSource Icon {
		get => icon;
		protected set {
			if (icon != value) {
				icon = value;
				OnPropertyChanged();
			}
		}
	}

	private ImageSource icon;

	public abstract string FullPath { get; }

	public string Name { get; protected set; }

	/// <summary>
	/// 类型描述
	/// </summary>
	public abstract string Type { get; }

	public long FileSize { get; protected init; }

	public bool IsFolder { get; protected init; }

	public bool IsSelected {
		get => isSelected;
		set {
			if (isSelected != value) {
				isSelected = value;
				OnPropertyChanged();
			}
		}
	}

	private bool isSelected;

	public SimpleCommand OpenCommand { get; protected init; }

	public SimpleCommand OpenInNewTabCommand { get; }

	public SimpleCommand OpenInNewWindowCommand { get; }

	public SimpleCommand ShowPropertiesCommand { get; }

	protected FileViewBaseItem(FileViewGridViewModel ownerViewModel) {
		OwnerViewModel = ownerViewModel;
		LostFocusCommand = new SimpleCommand(OnLostFocus);
		// ReSharper disable once AsyncVoidLambda
		OpenInNewTabCommand = new SimpleCommand(async _ => {
			if (IsFolder) {
				await OwnerViewModel.OwnerTabControl.OpenPathInNewTabAsync(FullPath);
			}
		});
		OpenInNewWindowCommand = new SimpleCommand(_ => new MainWindow(FullPath).Show());
		ShowPropertiesCommand = new SimpleCommand(_ => Win32Interop.ShowFileProperties(FullPath));
	}

	public abstract Task LoadIconAsync();

	public event PropertyChangedEventHandler PropertyChanged;

	[NotifyPropertyChangedInvocator]
	protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	#region 文件重命名
	public string EditingName { get; set; }

	/// <summary>
	/// 校验文件名是否有效
	/// </summary>
	public Func<string, OperationResult<bool>> VerifyFunc { get; } = fn => new OperationResult<bool> {
		Data = !FileUtils.IsProhibitedFileName(fn)
	};

	public bool IsErrorFileName { get; set; }

	public SimpleCommand LostFocusCommand { get; }

	/// <summary>
	/// 显示重命名的输入框
	/// </summary>
	public void BeginRename() {
		EditingName = Name;
		OnPropertyChanged(nameof(EditingName));
	}

	public void StopRename() {
		if (!IsErrorFileName && EditingName != Name) {
			if (Rename()) {
				Name = EditingName;
				OnPropertyChanged(nameof(Name));
			}
		}
		EditingName = null;
		OnPropertyChanged(nameof(EditingName));
	}

	public void OnLostFocus(object args) {
		StopRename();
	}

	/// <summary>
	/// 重命名，此时EditingName是新的名字
	/// </summary>
	protected abstract bool Rename();

	#endregion
}