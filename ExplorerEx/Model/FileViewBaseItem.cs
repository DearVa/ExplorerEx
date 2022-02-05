using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using ExplorerEx.Annotations;
using ExplorerEx.ViewModel;
using ExplorerEx.Win32;
using static ExplorerEx.Win32.IconHelper;

namespace ExplorerEx.Model; 

public abstract class FileViewBaseItem : INotifyPropertyChanged {
	protected FileViewTabViewModel OwnerViewModel { get; }

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

	public string Name { get; protected set; }

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

	protected FileViewBaseItem(FileViewTabViewModel ownerViewModel) {
		OwnerViewModel = ownerViewModel;
	}

	public abstract Task LoadIconAsync();

	public abstract Task RefreshAsync();

	public event PropertyChangedEventHandler PropertyChanged;

	[NotifyPropertyChangedInvocator]
	protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}