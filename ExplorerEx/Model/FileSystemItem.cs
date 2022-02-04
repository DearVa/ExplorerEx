using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ExplorerEx.Utils;
using ExplorerEx.View;
using ExplorerEx.ViewModel;
using ExplorerEx.Win32;
using static ExplorerEx.Win32.IconHelper;

namespace ExplorerEx.Model;

internal class FileSystemItem : FileViewBaseItem {
	public FileSystemInfo FileSystemInfo { get; }

	public DateTime LastWriteTime => FileSystemInfo.LastWriteTime;

	public string FileSizeString => FileUtils.FormatByteSize(FileSize);

	public string FullPath => FileSystemInfo.FullName;

	public SimpleCommand OpenCommand { get; }

	public SimpleCommand OpenInNewTabCommand { get; }

	public SimpleCommand OpenInNewWindowCommand { get; }

	public SimpleCommand ShowPropertiesCommand { get; }

	public FileSystemItem(FileViewTabViewModel ownerViewModel, FileSystemInfo fileSystemInfo) : base(ownerViewModel) {
		FileSystemInfo = fileSystemInfo;
		Name = FileSystemInfo.Name;
		if (fileSystemInfo is FileInfo fi) {
			FileSize = fi.Length;
			IsDirectory = false;
			Icon = UnknownTypeFileDrawingImage;
		} else {
			FileSize = -1;
			IsDirectory = true;
			LoadDirectoryIcon();
		}
		OpenCommand = new SimpleCommand(_ => Open());
		OpenInNewTabCommand = new SimpleCommand(_ => {
			if (IsDirectory) {
				OwnerViewModel.OwnerViewModel.OpenPathInNewTab(FullPath);
			}
		});
		OpenInNewWindowCommand = new SimpleCommand(_ => new MainWindow(FullPath).Show());
		ShowPropertiesCommand = new SimpleCommand(_ => Win32Interop.ShowFileProperties(FullPath));
	}

	public async Task Open() {
		if (IsDirectory) {
			await OwnerViewModel.LoadDirectoryAsync(FullPath);
		} else {
			try {
				Process.Start(new ProcessStartInfo {
					FileName = FullPath,
					UseShellExecute = true
				});
			} catch (Exception e) {
				HandyControl.Controls.MessageBox.Error(e.Message, "Fail to open file".L());
			}
		}
	}

	private void LoadDirectoryIcon() {
		try {
			if (Win32Interop.PathIsDirectoryEmpty(FileSystemInfo.FullName)) {
				Icon = EmptyFolderDrawingImage;
			} else {
				Icon = FolderDrawingImage;
			}
		} catch {
			Icon = EmptyFolderDrawingImage;
		}
	}

	public override async Task RefreshAsync() {
		LoadDirectoryIcon();
		await base.RefreshAsync();
	}

	public override async Task LoadIconAsync() {
		Debug.Assert(!IsDirectory);
		Icon = await GetPathIconAsync(FullPath, false, true, false);
	}
}