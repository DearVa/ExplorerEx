using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HandyControl.Data;
using HandyControl.Interactivity;
using HandyControl.Tools;

namespace HandyControl.Controls; 

public class TextBox : System.Windows.Controls.TextBox, IDataInput {
	public TextBox() {
		CommandBindings.Add(new CommandBinding(ControlCommands.Clear, (_, _) => {
			SetCurrentValue(TextProperty, string.Empty);
		}));
	}

	protected override void OnTextChanged(TextChangedEventArgs e) {
		base.OnTextChanged(e);
		VerifyData();
	}

	public static readonly DependencyProperty VerifyFuncProperty = DependencyProperty.Register(
		nameof(VerifyFunc), typeof(Func<string, OperationResult<bool>>), typeof(TextBox), new PropertyMetadata(default(Func<string, OperationResult<bool>>)));

	public Func<string, OperationResult<bool>> VerifyFunc {
		get => (Func<string, OperationResult<bool>>)GetValue(VerifyFuncProperty);
		set => SetValue(VerifyFuncProperty, value);
	}

	/// <summary>
	///     数据是否错误
	/// </summary>
	public static readonly DependencyProperty IsErrorProperty = DependencyProperty.Register(
		nameof(IsError), typeof(bool), typeof(TextBox), new PropertyMetadata(ValueBoxes.FalseBox));

	public bool IsError {
		get => (bool)GetValue(IsErrorProperty);
		set => SetValue(IsErrorProperty, ValueBoxes.BooleanBox(value));
	}

	/// <summary>
	///     错误提示
	/// </summary>
	public static readonly DependencyProperty ErrorPromptProperty = DependencyProperty.Register(
		nameof(ErrorPrompt), typeof(string), typeof(TextBox), new PropertyMetadata(default(string)));

	public string ErrorPrompt {
		get => (string)GetValue(ErrorPromptProperty);
		set => SetValue(ErrorPromptProperty, value);
	}

	/// <summary>
	///     文本类型
	/// </summary>
	public static readonly DependencyProperty TextTypeProperty = DependencyProperty.Register(
		nameof(TextType), typeof(TextType), typeof(TextBox), new PropertyMetadata(default(TextType)));

	public TextType TextType {
		get => (TextType)GetValue(TextTypeProperty);
		set => SetValue(TextTypeProperty, value);
	}

	/// <summary>
	///     是否显示清除按钮
	/// </summary>
	public static readonly DependencyProperty ShowClearButtonProperty = DependencyProperty.Register(
		nameof(ShowClearButton), typeof(bool), typeof(TextBox), new PropertyMetadata(ValueBoxes.FalseBox));

	public bool ShowClearButton {
		get => (bool)GetValue(ShowClearButtonProperty);
		set => SetValue(ShowClearButtonProperty, ValueBoxes.BooleanBox(value));
	}

	public virtual bool VerifyData() {
		OperationResult<bool> result;

		if (VerifyFunc != null) {
			result = VerifyFunc.Invoke(Text);
		} else {
			if (!string.IsNullOrEmpty(Text)) {
				if (TextType != TextType.Common) {
					var regexPattern = InfoElement.GetRegexPattern(this);
					result = string.IsNullOrEmpty(regexPattern)
						? Text.IsKindOf(TextType)
							? OperationResult.Success()
							: OperationResult.Failed(Properties.Langs.Lang.FormatError)
						: Text.IsKindOf(regexPattern)
							? OperationResult.Success()
							: OperationResult.Failed(Properties.Langs.Lang.FormatError);
				} else {
					result = OperationResult.Success();
				}
			} else if (InfoElement.GetNecessary(this)) {
				result = OperationResult.Failed(Properties.Langs.Lang.IsNecessary);
			} else {
				result = OperationResult.Success();
			}
		}

		var isError = !result.Data;
		if (isError) {
			SetCurrentValue(IsErrorProperty, ValueBoxes.TrueBox);
			SetCurrentValue(ErrorPromptProperty, result.Message);
		} else {
			isError = Validation.GetHasError(this);
			if (isError) {
				SetCurrentValue(ErrorPromptProperty, Validation.GetErrors(this)[0].ErrorContent?.ToString());
			} else {
				SetCurrentValue(IsErrorProperty, ValueBoxes.FalseBox);
				SetCurrentValue(ErrorPromptProperty, default(string));
			}
		}

		return !isError;
	}
}