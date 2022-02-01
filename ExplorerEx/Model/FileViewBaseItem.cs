using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using ExplorerEx.Annotations;

namespace ExplorerEx.Model; 

internal abstract class FileViewBaseItem : INotifyPropertyChanged {
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

	public bool IsDirectory { get; protected init; }

	public abstract Task LoadIconAsync();
	public event PropertyChangedEventHandler PropertyChanged;

	[NotifyPropertyChangedInvocator]
	protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}