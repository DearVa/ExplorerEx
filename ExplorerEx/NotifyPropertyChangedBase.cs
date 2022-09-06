using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ExplorerEx.Annotations;

namespace ExplorerEx;

public abstract class NotifyPropertyChangedBase : INotifyPropertyChanged {
	public event PropertyChangedEventHandler? PropertyChanged;

	[NotifyPropertyChangedInvocator]
	protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	protected void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
		if (EqualityComparer<T>.Default.Equals(field, value)) {
			return;
		}
		field = value;
		OnPropertyChanged(propertyName);
	}
}