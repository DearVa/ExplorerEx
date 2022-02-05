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

public class FileSystemItem : FileViewBaseItem {
	public FileSystemInfo FileSystemInfo { get; }

	public DateTime LastWriteTime => FileSystemInfo.LastWriteTime;

	public string FileTypeString => IsFolder ? (isEmptyFolder ? "Empty_folder".L() : "Folder".L()) : GetFileTypeDescription(Path.GetExtension(FileSystemInfo.Name));

	public string FileSizeString => FileUtils.FormatByteSize(FileSize);

	public string FullPath => FileSystemInfo.FullName;

	public SimpleCommand OpenCommand { get; }

	public SimpleCommand OpenInNewTabCommand { get; }

	public SimpleCommand OpenInNewWindowCommand { get; }

	public SimpleCommand ShowPropertiesCommand { get; }

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
		// ReSharper disable once AsyncVoidLambda
		OpenInNewTabCommand = new SimpleCommand(async _ => {
			if (IsFolder) {
				await OwnerViewModel.OwnerViewModel.OpenPathInNewTabAsync(FullPath);
			}
		});
		OpenInNewWindowCommand = new SimpleCommand(_ => new MainWindow(FullPath).Show());
		ShowPropertiesCommand = new SimpleCommand(_ => Win32Interop.ShowFileProperties(FullPath));
	}

	public async Task OpenAsync() {
		if (IsFolder) {
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
		Icon = await GetPathIconAsync(FullPath, false, true, false);
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