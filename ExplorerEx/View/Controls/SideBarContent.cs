using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using ExplorerEx.Converter;
using TextBox = System.Windows.Controls.TextBox;

namespace ExplorerEx.View.Controls;

[TemplatePart(Name = SearchToggleButtonKey, Type = typeof(ToggleButton))]
[TemplatePart(Name = SearchTextBoxKey, Type = typeof(TextBox))]
[TemplatePart(Name = DragAreaKey, Type = typeof(ContentPresenter))]
[TemplatePart(Name = DragTipPanelKey, Type = typeof(ContentPresenter))]
public class SideBarContent : Control {
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

	private const string SearchTextBoxKey = "SearchTextBox";
	private const string SearchToggleButtonKey = "SearchToggleButton";
	private const string DragAreaKey = "DragArea";
	private const string DragTipPanelKey = "DragTipPanel";

	private TextBox searchTextBox;
	private ContentPresenter dragTipPanel;

	public SideBarContent() {
		DataContext = this;
	}

	public override void OnApplyTemplate() {
		base.OnApplyTemplate();
		var searchToggleButton = (ToggleButton)GetTemplateChild(SearchToggleButtonKey)!;
		searchToggleButton.Checked += SearchToggleButton_OnChecked;
		searchToggleButton.Unchecked += SearchToggleButton_OnUnchecked;
		searchTextBox = (TextBox)GetTemplateChild(SearchTextBoxKey)!;
		searchTextBox.TextChanged += SearchTextBox_OnTextChanged;
		var dragArea = (ContentPresenter)GetTemplateChild(DragAreaKey)!;
		dragArea.Drop += DragArea_OnDrop;
		dragArea.DragEnter += DragArea_OnDragEnter;
		dragArea.DragLeave += DragArea_OnDragLeave;
		dragArea.DragOver += DragArea_OnDragOver;
		dragTipPanel = (ContentPresenter)GetTemplateChild(DragTipPanelKey)!;
	}

	private void SearchToggleButton_OnChecked(object sender, RoutedEventArgs e) {
		searchTextBox.Visibility = Visibility.Visible;
		searchTextBox.Focus();
		searchTextBox.SelectAll();
	}

	private void SearchToggleButton_OnUnchecked(object sender, RoutedEventArgs e) {
		searchTextBox.Visibility = Visibility.Collapsed;
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
		dragTipPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0d, TimeSpan.FromSeconds(0.1d)));
		FileDrop?.Invoke(fileList);
	}

	private void DragArea_OnDragEnter(object sender, DragEventArgs e) {
		if (e.Data.GetData(DataFormats.FileDrop) == null) {
			e.Effects = DragDropEffects.None;
			return;
		}
		dragTipPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(1d, TimeSpan.FromSeconds(0.1d)));
	}

	private void DragArea_OnDragLeave(object sender, DragEventArgs e) {
		dragTipPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0d, TimeSpan.FromSeconds(0.1d)));
	}

	private static void DragArea_OnDragOver(object sender, DragEventArgs e) {
		if (e.Data.GetData(DataFormats.FileDrop) == null) {
			e.Effects = DragDropEffects.None;
		}
	}
}