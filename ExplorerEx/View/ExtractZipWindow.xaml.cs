using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ExplorerEx.Utils;
using MessageBox = HandyControl.Controls.MessageBox;

namespace ExplorerEx.View;

/// <summary>
/// 提取压缩文件
/// </summary>
public partial class ExtractZipWindow {
	private readonly ZipArchive zipArchive;
	private CancellationTokenSource cts;

	private ExtractZipWindow(string zipPath) {
		zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Read, Encoding.GetEncoding("gb2312"));
		Title = "Extract".L() + ' ' + zipPath;
		InitializeComponent();
		DestPathTextBox.Text = Path.Combine(Path.GetDirectoryName(zipPath)!, Path.GetFileNameWithoutExtension(zipPath));
		DestPathTextBox.SelectAll();
	}

	public static void Show(string zipPath) {
		try {
			new ExtractZipWindow(zipPath).Show();
		} catch (Exception e) {
			Logger.Exception(e);
		}
	}

	protected override void OnClosed(EventArgs e) {
		base.OnClosed(e);
		zipArchive.Dispose();
	}

	private void BrowseButton_OnClick(object sender, RoutedEventArgs e) {
		var openFolderDialog = new OpenFolderDialog(DestPathTextBox.Text);
		if (openFolderDialog.ShowDialog()) {
			DestPathTextBox.Text = openFolderDialog.Folder;
		}
	}

	private void CancelButton_OnClick(object sender, RoutedEventArgs e) {
		cts?.Cancel();
		Close();
	}

	private async void ExtractButton_OnClick(object sender, RoutedEventArgs e) {
		var destination = DestPathTextBox.Text;
		try {
			Directory.CreateDirectory(destination);
		} catch (Exception ex) {
			Logger.Exception(ex);
			return;
		}
		DestGrid.IsEnabled = ExtractButton.IsEnabled = false;
		TotalProgressBar.Value = 0d;
		TotalProgressBar.Maximum = zipArchive.Entries.Count;
		var errorList = new List<string>();
		cts = new CancellationTokenSource();
		var token = cts.Token;
		await Task.Run(() => {  // 多线程解压貌似会出现 The archive entry was compressed using an unsupported compression method. 异常
			foreach (var zipArchiveEntry in zipArchive.Entries) {
				if (token.IsCancellationRequested) {
					return;
				}
				try {
					ExtractRelativeToDirectory(zipArchiveEntry, destination, true);
				} catch (Exception ex) {
					Logger.Error(zipArchiveEntry.FullName + '\n' + ex, false);
					errorList.Add(zipArchiveEntry.FullName);
				}
				Dispatcher.Invoke(() => TotalProgressBar.Value += 1d);
			}
		}, token);
		if (token.IsCancellationRequested) {
			return;
		}
		if (ShowWhenCompleteCheckBox.IsChecked.GetValueOrDefault()) {
			try {
				Process.Start(new ProcessStartInfo(destination) {
					UseShellExecute = true
				});
			} catch (Exception ex) {
				Logger.Exception(ex);
			}
		}
		if (errorList.Count > 0) {
			var sb = new StringBuilder();
			sb.Append("#ErrorExtractFiles".L()).Append('\n');
			var i = 0;
			for (; i < errorList.Count && i < 10; i++) {
				sb.Append(errorList[i]).Append('\n');
			}
			if (i < errorList.Count) {
				sb.Append('\n').Append(string.Format("And...More".L(), errorList.Count - i));
			} else {
				sb.Remove(sb.Length - 1, 1);
			}
			MessageBox.Error(sb.ToString());
		}
		Close();
	}

	private void ExtractRelativeToDirectory(ZipArchiveEntry source, string destinationDirectoryName, bool overwrite) {
		// Note that this will give us a good DirectoryInfo even if destinationDirectoryName exists:
		var di = Directory.CreateDirectory(destinationDirectoryName);
		var destinationDirectoryFullPath = di.FullName;
		if (!destinationDirectoryFullPath.EndsWith(Path.DirectorySeparatorChar)) {
			destinationDirectoryFullPath += Path.DirectorySeparatorChar;
		}

		var fileDestinationPath = Path.GetFullPath(Path.Combine(destinationDirectoryFullPath, source.FullName));

		if (!fileDestinationPath.StartsWith(destinationDirectoryFullPath)) {
			throw new IOException();
		}

		if (Path.GetFileName(fileDestinationPath).Length == 0) {
			// If it is a directory:
			if (source.Length != 0) {
				throw new IOException();
			}
			Directory.CreateDirectory(fileDestinationPath);
		} else {
			// If it is a file:
			// Create containing directory:
			var directoryName = Path.GetDirectoryName(fileDestinationPath)!;
			Directory.CreateDirectory(directoryName);
			Dispatcher.Invoke(() => ExtractingFileTextBlock.Text = fileDestinationPath);
			ExtractToFile(source, fileDestinationPath, overwrite);
		}
	}

	public void ExtractToFile(ZipArchiveEntry source, string destinationFileName, bool overwrite) {
		// Rely on FileStream's ctor for further checking destinationFileName parameter
		var fMode = overwrite ? FileMode.Create : FileMode.CreateNew;

		using (var fs = new FileStream(destinationFileName, fMode, FileAccess.Write, FileShare.None, 0x1000, false)) {
			var length = source.Length;
			if (length > 0) {  // 空文件就不执行了
				var buffer = ArrayPool<byte>.Shared.Rent((int)Math.Min(81920, length));

				var copyCount = source.Length / buffer.Length;
				var nCount = 1;
				while (copyCount > 128) {  // 如果拷贝次数太多，就减少回报次数，避免UI频繁更新
					copyCount /= 2;
					nCount *= 2;
				}
				Dispatcher.Invoke(() => {
					SingleProgressBar.Value = 0d;
					SingleProgressBar.Maximum = copyCount;
				});

				var n = 0;
				using var stream = source.Open();
				try {
					int bytesRead;
					while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0) {
						fs.Write(buffer, 0, bytesRead);
						n++;
						if (n >= nCount) {
							n = 0;
							Dispatcher.Invoke(() => SingleProgressBar.Value += 1d); // 复制的时候回报到进度条上
						}
					}
				} finally {
					ArrayPool<byte>.Shared.Return(buffer);
				}
			}
		}

		File.SetLastWriteTime(destinationFileName, source.LastWriteTime.DateTime);
	}
}