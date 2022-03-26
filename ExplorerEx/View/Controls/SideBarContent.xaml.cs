using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using ExplorerEx.Converter;

namespace ExplorerEx.View.Controls; 

public partial class SideBarContent {
	public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
		"Header", typeof(string), typeof(SideBarContent), new PropertyMetadata(default(string)));

	public string Header {
		get => (string)GetValue(HeaderProperty);
		set => SetValue(HeaderProperty, value);
	}

	public static readonly DependencyProperty HeaderContentProperty = DependencyProperty.Register(
		"HeaderContent", typeof(object), typeof(SideBarContent), new PropertyMetadata(default(object)));

	public object HeaderContent {
		get => GetValue(HeaderContentProperty);
		set => SetValue(HeaderContentProperty, value);
	}

	public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
		"Content", typeof(ItemsControl), typeof(SideBarContent), new PropertyMetadata(default(ItemsControl)));

	public ItemsControl Content {
		get => (ItemsControl)GetValue(ContentProperty);
		set => SetValue(ContentProperty, value);
	}

	public static readonly DependencyProperty ShowSearchButtonProperty = DependencyProperty.Register(
		"ShowSearchButton", typeof(bool), typeof(SideBarContent), new PropertyMetadata(true));

	/// <summary>
	/// 是否显示搜索按钮
	/// </summary>
	public bool ShowSearchButton {
		get => (bool)GetValue(ShowSearchButtonProperty);
		set => SetValue(ShowSearchButtonProperty, value);
	}

	public static readonly DependencyProperty DragTipProperty = DependencyProperty.Register(
		"DragTip", typeof(object), typeof(SideBarContent), new PropertyMetadata(default(object)));

	public object DragTip {
		get => GetValue(DragTipProperty);
		set => SetValue(DragTipProperty, value);
	}

	public static readonly DependencyProperty FilterProperty = DependencyProperty.Register(
		"Filter", typeof(StringFilter2VisibilityConverter), typeof(SideBarContent), new PropertyMetadata(default(StringFilter2VisibilityConverter)));

	public StringFilter2VisibilityConverter Filter {
		get => (StringFilter2VisibilityConverter)GetValue(FilterProperty);
		set => SetValue(FilterProperty, value);
	}

	public event Action<string[]> FileDrop;
	

	public SideBarContent() {
		DataContext = this;
		InitializeComponent();
	}

	private void SearchToggleButton_OnChecked(object sender, RoutedEventArgs e) {
		SearchTextBox.Visibility = Visibility.Visible;
		SearchTextBox.Focus();
		SearchTextBox.SelectAll();
	}

	private void SearchToggleButton_OnUnchecked(object sender, RoutedEventArgs e) {
		SearchTextBox.Visibility = Visibility.Collapsed;
	}

	private void SearchTextBox_OnTextChanged(object sender, TextChangedEventArgs e) {
		Filter.FilterString = ((TextBox)sender).Text;
		Content.Items.Refresh();
	}

	private void DragArea_OnDrop(object sender, DragEventArgs e) {
		if (e.Data.GetData(DataFormats.FileDrop) is not string[] fileList) {
			e.Effects = DragDropEffects.None;
			return;
		}
		DragTipGrid.BeginAnimation(OpacityProperty, new DoubleAnimation(0d, TimeSpan.FromSeconds(0.1d)));
		FileDrop?.Invoke(fileList);
	}

	private void DragArea_OnDragEnter(object sender, DragEventArgs e) {
		if (e.Data.GetData(DataFormats.FileDrop) == null) {
			e.Effects = DragDropEffects.None;
			return;
		}
		DragTipGrid.BeginAnimation(OpacityProperty, new DoubleAnimation(1d, TimeSpan.FromSeconds(0.1d)));
	}

	private void DragArea_OnDragLeave(object sender, DragEventArgs e) {
		DragTipGrid.BeginAnimation(OpacityProperty, new DoubleAnimation(0d, TimeSpan.FromSeconds(0.1d)));
	}

	private void DragArea_OnDragOver(object sender, DragEventArgs e) {
		if (e.Data.GetData(DataFormats.FileDrop) == null) {
			e.Effects = DragDropEffects.None;
		}
	}
}