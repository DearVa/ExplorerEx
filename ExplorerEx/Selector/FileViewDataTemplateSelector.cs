using System;
using System.Windows;
using System.Windows.Controls;
using ExplorerEx.ViewModel;

namespace ExplorerEx.Selector;

public class FileViewDataTemplateSelector : DataTemplateSelector {
	public enum Type {
		Home,
		Detail
	}

	public DataTemplate FileViewHomeTemplate { get; set; }
	public DataTemplate FileViewDetailTemplate { get; set; }

	public override DataTemplate SelectTemplate(object item, DependencyObject container) {
		var vm = (FileViewTabViewModel)item!;
		vm.FileViewContentPresenter = (ContentPresenter)container;
		return vm.Type switch {
			Type.Home => FileViewHomeTemplate,
			Type.Detail => FileViewDetailTemplate,
			_ => throw new ArgumentOutOfRangeException(nameof(item))
		};
	}
}