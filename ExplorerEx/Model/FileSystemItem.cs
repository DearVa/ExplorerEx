using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ExplorerEx.Utils;
using ExplorerEx.ViewModel;
using ExplorerEx.Win32;
using static ExplorerEx.Win32.IconHelper;

namespace ExplorerEx.Model;

public class FileSystemItem : FileViewBaseItem {
	public FileSystemInfo FileSystemInfo { get; }

	public DateTime LastWriteTime => FileSystemInfo.LastWriteTime;

	/// <summary>
	/// 类型
	/// </summary>
	public override string Type => IsFolder ? (isEmptyFolder ? "Empty_folder".L() : "Folder".L()) : GetFileTypeDescription(Path.GetExtension(FileSystemInfo.Name));

	public string FileSizeString => FileUtils.FormatByteSize(FileSize);

	public override string FullPath => FileSystemInfo.FullName;

	/// <summary>
	/// 是否使用大图标
	/// </summary>
	public bool UseLargeIcon { get; set; }

	private bool isEmptyFolder;

	public FileSystemItem(FileViewTabViewModel ownerViewModel, FileSystemInfo fileSystemInfo) : base(ownerViewModel) {
		FileSystemInfo = fileSystemInfo;
		Name = FileSystemInfo.Name;
		if (fileSystemInfo is FileInfo fi) {
			FileSize = fi.Length;
			IsFolder = false;
			Icon = UnknownTypeFileDrawingImage;
		} else {
			FileSize = -1;
			IsFolder = true;
			LoadDirectoryIcon();
		}
		// ReSharper disable once AsyncVoidLambda
		OpenCommand = new SimpleCommand(async _ => await OpenAsync());
	}

	/// <summary>
	/// 打开该文件或者文件夹
	/// </summary>
	/// <param name="runAs">以管理员身份运行，只对文件有效</param>
	/// <returns></returns>
	public async Task OpenAsync(bool runAs = false) {
		if (IsFolder) {
			await OwnerViewModel.LoadDirectoryAsync(FullPath);
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

	private void LoadDirectoryIcon() {
		try {
			isEmptyFolder = Win32Interop.PathIsDirectoryEmpty(FileSystemInfo.FullName);
			if (isEmptyFolder) {
				Icon = EmptyFolderDrawingImage;
			} else {
				Icon = FolderDrawingImage;
			}
		} catch {
			Icon = EmptyFolderDrawingImage;
		}
	}

	public override async Task LoadIconAsync() {
		Debug.Assert(!IsFolder);
		if (UseLargeIcon) {
			Icon = await GetPathThumbnailAsync(FullPath);
		} else {
			Icon = await GetPathIconAsync(FullPath, true);
		}
	}

	protected override bool Rename() {
		if (EditingName == null) {
			return false;
		}
		var basePath = Path.GetDirectoryName(FullPath);
		if (Path.GetExtension(FullPath) != Path.GetExtension(EditingName)) {
			if (!MessageBoxHelper.AskWithDefault("RenameExtension", "Are_you_sure_to_change_extension".L())) {
				return false;
			}
		}
		try {
			FileUtils.FileOperation(Win32Interop.FileOpType.Rename, FullPath, Path.Combine(basePath!, EditingName));
			return true;
		} catch (Exception e) {
			Logger.Exception(e);
		}
		return false;
	}

	public async Task RefreshAsync() {
		if (IsFolder) {
			LoadDirectoryIcon();
		} else {
			await LoadIconAsync();
			OnPropertyChanged(nameof(FileSize));
		}
		OnPropertyChanged(nameof(Icon));
	}
}