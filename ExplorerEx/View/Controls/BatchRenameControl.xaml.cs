using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ExplorerEx.Command;
using ExplorerEx.Model;
using ExplorerEx.Model.BatchRename;
using ExplorerEx.Utils;
using ExplorerEx.Utils.Collections;

namespace ExplorerEx.View.Controls;

/// <summary>
/// BatchRename.xaml 的交互逻辑
/// </summary>
internal partial class BatchRenameControl {
	/// <summary>
	/// 需要被重命名的项目
	/// </summary>
	public ConcurrentObservableCollection<BatchRenameItem> Items { get; } = new();

	private readonly FrameworkElement[] dockingTargets;

	/// <summary>
	/// 文本替换
	/// </summary>
	public ConcurrentObservableCollection<ReplaceTextItem> ReplaceTextItems { get; } = new();

	public Shortcut[] Shortcuts => shortcuts ??= new[] {
		new Shortcut("Lowercase2Uppercase", s => s.ToUpper()),
		new Shortcut("Uppercase2Lowercase", s => s.ToLower()),
	};

	private Shortcut[]? shortcuts;

	/// <summary>
	/// 是否改变过
	/// </summary>
	private bool isModified;

	/// <summary>
	/// 通用的cts
	/// </summary>
	private CancellationTokenSource cts = new();

	/// <summary>
	/// 用于应用更改到文件列表的Task
	/// </summary>
	private Task? applyTask;

	private BatchRenameControl(IEnumerable<FileListViewItem> items) {
		Items.AddRange(items.Select(i => new BatchRenameItem(i)));

		AddReplaceTextItem();

		DataContext = this;
		InitializeComponent();

		dockingTargets = new FrameworkElement[] { SimplePanel0, SimplePanel1, SimplePanel2 };
	}

	private void TabControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
		if (isModified) {
			cts.Cancel();
			cts = new CancellationTokenSource();
			applyTask = Task.Run(() => {
				foreach (var item in Items) {
					if (cts.IsCancellationRequested) {
						return;
					}
					item.ReplacedName = item.Item.DisplayText;
				}
				isModified = false;  // 要放在Task里面
			}, cts.Token);
		}
		if (DockingTarget != null) {
			DockingTarget.Target = dockingTargets[TabControl.SelectedIndex];
		}
	}

	#region 添加序号

	public static readonly DependencyProperty LeftTextProperty = DependencyProperty.Register(
		nameof(LeftText), typeof(string), typeof(BatchRenameControl), new PropertyMetadata(default(string), Sequence_OnPropertyChanged));

	public string LeftText {
		get => (string)GetValue(LeftTextProperty);
		set => SetValue(LeftTextProperty, value);
	}

	public static readonly DependencyProperty RightTextProperty = DependencyProperty.Register(
		nameof(RightText), typeof(string), typeof(BatchRenameControl), new PropertyMetadata(default(string), Sequence_OnPropertyChanged));

	public string RightText {
		get => (string)GetValue(RightTextProperty);
		set => SetValue(RightTextProperty, value);
	}

	public static readonly DependencyProperty StartingNumberProperty = DependencyProperty.Register(
		nameof(StartingNumber), typeof(string), typeof(BatchRenameControl), new PropertyMetadata("1", Sequence_OnPropertyChanged));

	public string StartingNumber {
		get => (string)GetValue(StartingNumberProperty);
		set => SetValue(StartingNumberProperty, value);
	}

	public static readonly DependencyProperty FixedNumberProperty = DependencyProperty.Register(
		nameof(FixedNumber), typeof(string), typeof(BatchRenameControl), new PropertyMetadata("0", Sequence_OnPropertyChanged));

	public string FixedNumber {
		get => (string)GetValue(FixedNumberProperty);
		set => SetValue(FixedNumberProperty, value);
	}

	private static void Sequence_OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		((BatchRenameControl)d).ApplySequenceChange();
	}

	/// <summary>
	/// 应用“添加序号”更改到文件列表
	/// </summary>
	private void ApplySequenceChange() {
		if (!int.TryParse(StartingNumber, out var startingNumber) || startingNumber < 0) {
			return;
		}
		if (!int.TryParse(FixedNumber, out var fixedNumber) || fixedNumber is < 0 or > 16) {
			return;
		}
		var leftText = LeftText;
		var rightText = RightText;
		cts.Cancel();
		cts = new CancellationTokenSource();
		applyTask = Task.Run(() => {
			for (var i = 0; i < Items.Count; i++) {
				if (cts.IsCancellationRequested) {
					return;
				}
				var item = Items[i];
				item.ReplacedName = leftText + (i + startingNumber).ToString("D" + fixedNumber) + rightText;
			}
		}, cts.Token);
		isModified = true;
	}

	#endregion

	#region 文本替换

	/// <summary>
	/// 执行文本替换
	/// </summary>
	private void DoReplaceText() {
		cts.Cancel();
		cts = new CancellationTokenSource();
		applyTask = Task.Run(() => {
			foreach (var item in Items) {
				if (cts.IsCancellationRequested) {
					return;
				}
				item.ReplacedName = ReplaceTextItems.Aggregate(item.Item.DisplayText, (current, replaceTextItem) => replaceTextItem.Replace(current));
			}
		}, cts.Token);
		isModified = true;
	}

	private void AddReplaceTextItem() {
		var replaceTextItem = new ReplaceTextItem();
		replaceTextItem.Changed += DoReplaceText;
		ReplaceTextItems.Add(replaceTextItem);
	}

	private void AddReplaceTextItemButton_OnClick(object sender, RoutedEventArgs e) {
		AddReplaceTextItem();
	}

	private void RemoveReplaceTextItemButton_OnClick(object sender, RoutedEventArgs e) {
		ReplaceTextItems.Remove((ReplaceTextItem)((Button)sender).DataContext);
	}

	#endregion

	#region 快捷操作

	private void ShortcutsCombobox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
		if (e.AddedItems.Count > 0) {
			cts.Cancel();
			cts = new CancellationTokenSource();
			applyTask = Task.Run(() => {
				foreach (var item in Items) {
					if (cts.IsCancellationRequested) {
						return;
					}
					item.ReplacedName = ((Shortcut)e.AddedItems[0]!).Func.Invoke(item.Item.DisplayText);
				}
			}, cts.Token);
			isModified = true;
		}
		e.Handled = true;
	}

	#endregion

	/// <summary>
	/// 最终应用重命名，如果此时可以关闭，那就返回true；返回false表示不用关闭
	/// </summary>
	/// <param name="owner"></param>
	private async Task<bool> ApplyRename(MainWindow owner) {
		if (applyTask is { IsCompleted: false }) {
			await applyTask;  // 如果当前有任务，那就等待任务执行完
		}
		var items = Items.Where(i => i.Item.DisplayText != i.ReplacedName).ToList();
		if (items.Count == 0) {
			ContentDialog.Error("#BatchRenameErrorNotModified");
			return false;
		}
		foreach (var item in items) {
			if (FileUtils.IsProhibitedFileName(item.ReplacedName, out var msg)) {
				ContentDialog.Error(msg!);
				RenameItemsListBox.ScrollIntoView(item);
				return false;
			}
		}
		var failedList = new List<(string, Exception)>();
		await Task.Run(() => {
			foreach (var item in items) {
				try {
					item.Item.Rename(item.ReplacedName);
				} catch (Exception e) {
					failedList.Add((item.Item.FullPath, e));
				}
			}
		});
		new ContentDialog {
			Title = "BatchRenameResult".L(),
			Content = string.Format("#BatchRenameResult".L(), items.Count - failedList.Count, failedList.Count)
		}.Show(owner);
		return true;
	}

	/// <summary>
	/// 在ContentDialog中展示
	/// </summary>
	/// <param name="items"></param>
	/// <param name="owner"></param>
	public static void Show(IEnumerable<FileListViewItem> items, MainWindow owner) {
		var brc = new BatchRenameControl(items);
		var command = new SimpleCommand(async e => {
			var args = (CancelEventArgs)e!;
			args.Cancel = !await brc.ApplyRename(owner);
		});
		new ContentDialog {
			Title = "BatchRename".L(),
			Content = brc,
			PrimaryButtonText = "Ok".L(),
			PrimaryButtonCommand = command,
			CancelButtonText = "Cancel".L()
		}.Show(owner);
	}
}