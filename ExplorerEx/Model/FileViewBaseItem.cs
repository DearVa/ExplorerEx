﻿using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using ExplorerEx.Annotations;
using ExplorerEx.ViewModel;

namespace ExplorerEx.Model; 

internal abstract class FileViewBaseItem : INotifyPropertyChanged {
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

	public bool IsDirectory { get; protected init; }

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

	public virtual async Task RefreshAsync() {
		if (!IsDirectory) {
			await LoadIconAsync();
		}
		OnPropertyChanged(nameof(Icon));
		OnPropertyChanged(nameof(FileSize));
	}

	public event PropertyChangedEventHandler PropertyChanged;

	[NotifyPropertyChangedInvocator]
	protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}