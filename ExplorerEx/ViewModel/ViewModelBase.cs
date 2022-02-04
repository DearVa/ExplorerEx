using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using ExplorerEx.Annotations;

namespace ExplorerEx.ViewModel;

public class ViewModelBase : INotifyPropertyChanged {
	public event PropertyChangedEventHandler PropertyChanged;

	[NotifyPropertyChangedInvocator]
	protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}