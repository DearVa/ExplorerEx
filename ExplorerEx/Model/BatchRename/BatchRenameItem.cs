namespace ExplorerEx.Model.BatchRename;

internal class BatchRenameItem : NotifyPropertyChangedBase {
	public FileListViewItem Item {
		get => item;
		set => SetField(ref item, value);
	}

	private FileListViewItem item;

	public string ReplacedName {
		get => replacedName;
		set => SetField(ref replacedName, value);
	}

	private string replacedName;

	public BatchRenameItem(FileListViewItem item) {
		this.item = item;
		this.replacedName = item.DisplayText;
	}
}