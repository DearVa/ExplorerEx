using HandyControl.Data;
using HandyControl.Interactivity;
using HandyControl.Properties.Langs;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace HandyControl.Controls;

[TemplatePart(Name = AutoCompletePanel, Type = typeof(Panel))]
[TemplatePart(Name = EditableTextBox, Type = typeof(System.Windows.Controls.TextBox))]
[TemplatePart(Name = AutoPopupAutoComplete, Type = typeof(Popup))]
public class ComboBox : System.Windows.Controls.ComboBox, IDataInput {
	private bool isAutoCompleteAction = true;

	private Panel autoCompletePanel;

	private System.Windows.Controls.TextBox editableTextBox;

	private Popup autoPopupAutoComplete;

	private const string AutoCompletePanel = "PART_AutoCompletePanel";

	private const string AutoPopupAutoComplete = "PART_Popup_AutoComplete";

	private const string EditableTextBox = "PART_EditableTextBox";

	public ComboBox() {
		CommandBindings.Add(new CommandBinding(ControlCommands.Clear, (_, _) => {
			SetCurrentValue(SelectedValueProperty, null);
			SetCurrentValue(SelectedItemProperty, null);
			SetCurrentValue(SelectedIndexProperty, -1);
			SetCurrentValue(TextProperty, "");
		}));
	}

	public override void OnApplyTemplate() {
		if (editableTextBox != null) {
			BindingOperations.ClearBinding(editableTextBox, System.Windows.Controls.TextBox.TextProperty);
			editableTextBox.GotFocus -= EditableTextBox_GotFocus;
			editableTextBox.LostFocus -= EditableTextBox_LostFocus;
		}

		base.OnApplyTemplate();

		if (IsEditable) {
			editableTextBox = GetTemplateChild(EditableTextBox) as System.Windows.Controls.TextBox;

			if (editableTextBox != null) {
				editableTextBox.TextChanged += EditableTextBox_TextChanged;

				editableTextBox.SetBinding(SelectionBrushProperty, new Binding(SelectionBrushProperty.Name) { Source = this });
				editableTextBox.SetBinding(SelectionTextBrushProperty, new Binding(SelectionTextBrushProperty.Name) { Source = this });
				editableTextBox.SetBinding(SelectionOpacityProperty, new Binding(SelectionOpacityProperty.Name) { Source = this });
				editableTextBox.SetBinding(CaretBrushProperty, new Binding(CaretBrushProperty.Name) { Source = this });

				if (AutoComplete) {
					autoPopupAutoComplete = GetTemplateChild(AutoPopupAutoComplete) as Popup;
					autoCompletePanel = GetTemplateChild(AutoCompletePanel) as Panel;
					editableTextBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, new Binding(SearchTextProperty.Name) {
						UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
						Mode = BindingMode.OneWayToSource,
						Delay = 500,
						Source = this
					});
					editableTextBox.GotFocus += EditableTextBox_GotFocus;
					editableTextBox.LostFocus += EditableTextBox_LostFocus;
				}
			}
		}
	}

	private void EditableTextBox_TextChanged(object sender, TextChangedEventArgs e) {
		VerifyData();
	}

	private void EditableTextBox_LostFocus(object sender, RoutedEventArgs e) {
		if (autoPopupAutoComplete != null) {
			autoPopupAutoComplete.IsOpen = false;
		}
	}

	protected override void OnDropDownClosed(EventArgs e) {
		base.OnDropDownClosed(e);

		isAutoCompleteAction = false;
	}

	private void EditableTextBox_GotFocus(object sender, RoutedEventArgs e) {
		if (autoPopupAutoComplete != null && editableTextBox != null &&
			!string.IsNullOrEmpty(editableTextBox.Text)) {
			autoPopupAutoComplete.IsOpen = true;
		}
	}

	protected override void OnSelectionChanged(SelectionChangedEventArgs e) {
		isAutoCompleteAction = false;
		base.OnSelectionChanged(e);
		VerifyData();
	}

	public static readonly DependencyProperty AddItemButtonTextProperty = DependencyProperty.Register(
		"AddItemButtonText", typeof(string), typeof(ComboBox), new PropertyMetadata(default(string)));

	public string AddItemButtonText {
		get => (string)GetValue(AddItemButtonTextProperty);
		set => SetValue(AddItemButtonTextProperty, value);
	}

	/// <summary>
	///     数据验证委托
	/// </summary>
	public Func<string, OperationResult<bool>> VerifyFunc { get; set; }

	/// <summary>
	///     数据搜索委托
	/// </summary>
	public Func<ItemCollection, object, IEnumerable<object>> SearchFunc { get; set; }

	/// <summary>
	///     数据是否错误
	/// </summary>
	public static readonly DependencyProperty IsErrorProperty = DependencyProperty.Register(
		"IsError", typeof(bool), typeof(ComboBox), new PropertyMetadata(ValueBoxes.FalseBox));

	/// <summary>
	///     数据是否错误
	/// </summary>
	public bool IsError {
		get => (bool)GetValue(IsErrorProperty);
		set => SetValue(IsErrorProperty, ValueBoxes.BooleanBox(value));
	}

	/// <summary>
	///     错误提示
	/// </summary>
	public static readonly DependencyProperty ErrorStrProperty = DependencyProperty.Register(
		"ErrorStr", typeof(string), typeof(ComboBox), new PropertyMetadata(default(string)));

	/// <summary>
	///     错误提示
	/// </summary>
	public string ErrorStr {
		get => (string)GetValue(ErrorStrProperty);
		set => SetValue(ErrorStrProperty, value);
	}

	/// <summary>
	///     文本类型
	/// </summary>
	public static readonly DependencyPropertyKey TextTypePropertyKey =
		DependencyProperty.RegisterReadOnly("TextType", typeof(TextType), typeof(ComboBox),
			new PropertyMetadata(default(TextType)));

	/// <summary>
	///     文本类型
	/// </summary>
	public static readonly DependencyProperty TextTypeProperty = TextTypePropertyKey.DependencyProperty;

	/// <summary>
	///     文本类型
	/// </summary>
	public TextType TextType {
		get => (TextType)GetValue(TextTypeProperty);
		set => SetValue(TextTypeProperty, value);
	}

	/// <summary>
	///     是否显示清除按钮
	/// </summary>
	public static readonly DependencyProperty ShowClearButtonProperty = DependencyProperty.Register(
		"ShowClearButton", typeof(bool), typeof(ComboBox), new PropertyMetadata(ValueBoxes.FalseBox));

	/// <summary>
	///     是否显示清除按钮
	/// </summary>
	public bool ShowClearButton {
		get => (bool)GetValue(ShowClearButtonProperty);
		set => SetValue(ShowClearButtonProperty, ValueBoxes.BooleanBox(value));
	}

	/// <summary>
	///     是否自动完成输入
	/// </summary>
	public static readonly DependencyProperty AutoCompleteProperty = DependencyProperty.Register(
		"AutoComplete", typeof(bool), typeof(ComboBox), new PropertyMetadata(ValueBoxes.FalseBox, OnAutoCompleteChanged));

	private static void OnAutoCompleteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var ctl = (ComboBox)d;
		if (ctl.editableTextBox != null) {
			ctl.UpdateSearchItems(ctl.editableTextBox.Text);
		}
	}

	/// <summary>
	///     是否自动完成输入
	/// </summary>
	public bool AutoComplete {
		get => (bool)GetValue(AutoCompleteProperty);
		set => SetValue(AutoCompleteProperty, ValueBoxes.BooleanBox(value));
	}

	/// <summary>
	///     搜索文本
	/// </summary>
	internal static readonly DependencyProperty SearchTextProperty = DependencyProperty.Register(
		"SearchText", typeof(string), typeof(ComboBox), new PropertyMetadata(default(string), OnSearchTextChanged));

	private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var ctl = (ComboBox)d;
		if (ctl.isAutoCompleteAction) {
			ctl.UpdateSearchItems(e.NewValue as string);
		}

		ctl.isAutoCompleteAction = true;
	}

	/// <summary>
	///     搜索文本
	/// </summary>
	internal string SearchText {
		get => (string)GetValue(SearchTextProperty);
		set => SetValue(SearchTextProperty, value);
	}

	public static readonly DependencyProperty SelectionBrushProperty =
		TextBoxBase.SelectionBrushProperty.AddOwner(typeof(ComboBox));

	public Brush SelectionBrush {
		get => (Brush)GetValue(SelectionBrushProperty);
		set => SetValue(SelectionBrushProperty, value);
	}

	public static readonly DependencyProperty SelectionTextBrushProperty =
		TextBoxBase.SelectionTextBrushProperty.AddOwner(typeof(ComboBox));

	public Brush SelectionTextBrush {
		get => (Brush)GetValue(SelectionTextBrushProperty);
		set => SetValue(SelectionTextBrushProperty, value);
	}

	public static readonly DependencyProperty SelectionOpacityProperty =
		TextBoxBase.SelectionOpacityProperty.AddOwner(typeof(ComboBox));

	public double SelectionOpacity {
		get => (double)GetValue(SelectionOpacityProperty);
		set => SetValue(SelectionOpacityProperty, value);
	}

	public static readonly DependencyProperty CaretBrushProperty =
		TextBoxBase.CaretBrushProperty.AddOwner(typeof(ComboBox));

	public Brush CaretBrush {
		get => (Brush)GetValue(CaretBrushProperty);
		set => SetValue(CaretBrushProperty, value);
	}

	/// <summary>
	///     验证数据
	/// </summary>
	/// <returns></returns>
	public virtual bool VerifyData() {
		OperationResult<bool> result;

		var value = editableTextBox == null ? Text : editableTextBox.Text;

		if (VerifyFunc != null) {
			result = VerifyFunc.Invoke(value);
		} else {
			if (!string.IsNullOrEmpty(value)) {
				result = OperationResult.Success();
			} else if (InfoElement.GetNecessary(this)) {
				result = OperationResult.Failed(Lang.IsNecessary);
			} else {
				result = OperationResult.Success();
			}
		}

		var isError = !result.Data;
		if (isError) {
			SetCurrentValue(IsErrorProperty, ValueBoxes.TrueBox);
			SetCurrentValue(ErrorStrProperty, result.Message);
		} else {
			isError = Validation.GetHasError(this);
			if (isError) {
				SetCurrentValue(ErrorStrProperty, Validation.GetErrors(this)[0].ErrorContent?.ToString());
			} else {
				SetCurrentValue(IsErrorProperty, ValueBoxes.FalseBox);
				SetCurrentValue(ErrorStrProperty, default(string));
			}
		}

		return !isError;
	}

	/// <summary>
	///     更新搜索的项目
	/// </summary>
	/// <param name="key"></param>
	private void UpdateSearchItems(string key) {
		if (editableTextBox != null && autoPopupAutoComplete != null) {
			autoPopupAutoComplete.IsOpen = !string.IsNullOrEmpty(key);
			autoCompletePanel.Children.Clear();

			if (SearchFunc == null) {
				if (!string.IsNullOrEmpty(key)) {
					foreach (var item in Items) {
						var content = item?.ToString();
						if (content == null) {
							continue;
						}
						if (!content.Contains(key)) {
							continue;
						}

						autoCompletePanel.Children.Add(CreateSearchItem(item));
					}
				}
			} else {
				foreach (var item in SearchFunc.Invoke(Items, key)) {
					autoCompletePanel.Children.Add(CreateSearchItem(item));
				}
			}
		}
	}

	private ComboBoxItem CreateSearchItem(object content) {
		var item = new ComboBoxItem {
			Content = content,
			Style = ItemContainerStyle,
			ContentTemplate = ItemTemplate
		};

		item.PreviewMouseLeftButtonDown += AutoCompleteItem_PreviewMouseLeftButtonDown;

		return item;
	}

	private void AutoCompleteItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
		if (sender is ComboBoxItem comboBoxItem) {
			if (autoPopupAutoComplete != null) {
				autoPopupAutoComplete.IsOpen = false;
			}

			isAutoCompleteAction = false;
			SelectedValue = comboBoxItem.Content;
		}
	}
}