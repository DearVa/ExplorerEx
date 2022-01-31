using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Media;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HandyControl.Data;
using HandyControl.Interactivity;
using HandyControl.Properties.Langs;
using HandyControl.Tools;
using HandyControl.Tools.Interop;

namespace HandyControl.Controls {
	/// <summary>
	///     消息框
	/// </summary>
	[TemplatePart(Name = ElementPanel, Type = typeof(Panel))]
	[TemplatePart(Name = ElementButtonClose, Type = typeof(Button))]

	public sealed class MessageBox : Window {
		private const string ElementPanel = "PART_Panel";

		private const string ElementButtonClose = "PART_ButtonClose";

		private Button _buttonClose;

		private Panel _panel;

		private MessageBoxResult _messageBoxResult = MessageBoxResult.Cancel;

		private Button _buttonOk;

		private Button _buttonCancel;

		private Button _buttonYes;

		private Button _buttonNo;

		private bool _showOk;

		private bool _showCancel;

		private bool _showYes;

		private bool _showNo;

		private IntPtr _lastActiveWindowIntPtr;

		/// <summary>
		/// 获取一个值，当前是否有MessageBox显示
		/// </summary>
		public static bool IsShown => showCount != 0;

		private static int showCount;

		public static readonly DependencyProperty OkButtonTextProperty = DependencyProperty.Register(
			"OkButtonText", typeof(string), typeof(MessageBox), new PropertyMetadata(Lang.Confirm));

		public string OkButtonText {
			get => (string)GetValue(OkButtonTextProperty);
			set => SetValue(OkButtonTextProperty, value);
		}

		public static readonly DependencyProperty CancelButtonTextProperty = DependencyProperty.Register(
			"CancelButtonText", typeof(string), typeof(MessageBox), new PropertyMetadata(Lang.Cancel));

		public string CancelButtonText {
			get => (string)GetValue(CancelButtonTextProperty);
			set => SetValue(CancelButtonTextProperty, value);
		}

		public static readonly DependencyProperty YesButtonTextProperty = DependencyProperty.Register(
			"YesButtonText", typeof(string), typeof(MessageBox), new PropertyMetadata(Lang.Yes));

		public string YesButtonText {
			get => (string)GetValue(YesButtonTextProperty);
			set => SetValue(YesButtonTextProperty, value);
		}

		public static readonly DependencyProperty NoButtonTextProperty = DependencyProperty.Register(
			"NoButtonText", typeof(string), typeof(MessageBox), new PropertyMetadata(Lang.No));

		public string NoButtonText {
			get => (string)GetValue(NoButtonTextProperty);
			set => SetValue(NoButtonTextProperty, value);
		}

		public static readonly DependencyProperty CheckBoxVisibilityProperty = DependencyProperty.Register(
			"CheckBoxVisibility", typeof(Visibility), typeof(MessageBox), new PropertyMetadata(Visibility.Collapsed));

		public Visibility CheckBoxVisibility {
			get => (Visibility)GetValue(CheckBoxVisibilityProperty);
			set => SetValue(CheckBoxVisibilityProperty, value);
		}

		public static readonly DependencyProperty CheckBoxTextProperty = DependencyProperty.Register(
			"CheckBoxText", typeof(string), typeof(MessageBox), new PropertyMetadata(default(string)));

		public string CheckBoxText {
			get => (string)GetValue(CheckBoxTextProperty);
			set => SetValue(CheckBoxTextProperty, value);
		}

		public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
			"IsChecked", typeof(bool?), typeof(MessageBox), new PropertyMetadata(default(bool?)));

		public bool? IsChecked {
			get => (bool?)GetValue(IsCheckedProperty);
			set => SetValue(IsCheckedProperty, value);
		}

		public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
			"Message", typeof(string), typeof(MessageBox), new PropertyMetadata(default(string)));

		public string Message {
			get => (string)GetValue(MessageProperty);
			set => SetValue(MessageProperty, value);
		}

		public static readonly DependencyProperty ImageProperty = DependencyProperty.Register(
			"Image", typeof(Geometry), typeof(MessageBox), new PropertyMetadata(default(Geometry)));

		public Geometry Image {
			get => (Geometry)GetValue(ImageProperty);
			set => SetValue(ImageProperty, value);
		}

		public static readonly DependencyProperty ImageBrushProperty = DependencyProperty.Register(
			"ImageBrush", typeof(Brush), typeof(MessageBox), new PropertyMetadata(default(Brush)));

		public Brush ImageBrush {
			get => (Brush)GetValue(ImageBrushProperty);
			set => SetValue(ImageBrushProperty, value);
		}

		public static readonly DependencyProperty ShowImageProperty = DependencyProperty.Register(
			"ShowImage", typeof(bool), typeof(MessageBox), new PropertyMetadata(ValueBoxes.FalseBox));

		public bool ShowImage {
			get => (bool)GetValue(ShowImageProperty);
			set => SetValue(ShowImageProperty, ValueBoxes.BooleanBox(value));
		}

		private MessageBox() {
			CommandBindings.Add(new CommandBinding(ControlCommands.Confirm, (s, e) => {
				_messageBoxResult = MessageBoxResult.OK;
				Close();
			}, (s, e) => e.CanExecute = _showOk));
			CommandBindings.Add(new CommandBinding(ControlCommands.Cancel, (s, e) => {
				_messageBoxResult = MessageBoxResult.Cancel;
				Close();
			}, (s, e) => e.CanExecute = _showCancel));
			CommandBindings.Add(new CommandBinding(ControlCommands.Yes, (s, e) => {
				_messageBoxResult = MessageBoxResult.Yes;
				Close();
			}, (s, e) => e.CanExecute = _showYes));
			CommandBindings.Add(new CommandBinding(ControlCommands.No, (s, e) => {
				_messageBoxResult = MessageBoxResult.No;
				Close();
			}, (s, e) => e.CanExecute = _showNo));
		}

		protected override void OnSourceInitialized(EventArgs e) {
			if (_showYes && !_showCancel) {
				var hMenu = InteropMethods.GetSystemMenu(this.GetHandle(), false);
				if (hMenu != IntPtr.Zero) {
					InteropMethods.EnableMenuItem(hMenu, InteropValues.SC_CLOSE, InteropValues.MF_BYCOMMAND | InteropValues.MF_GRAYED);
				}

				if (_buttonClose != null) {
					_buttonClose.IsEnabled = false;
				}
			}

			base.OnSourceInitialized(e);

			_lastActiveWindowIntPtr = InteropMethods.GetForegroundWindow();
			Activate();
		}

		protected override void OnClosed(EventArgs e) {
			InteropMethods.SetForegroundWindow(_lastActiveWindowIntPtr);

			base.OnClosed(e);
		}

		public override void OnApplyTemplate() {
			base.OnApplyTemplate();

			_panel = GetTemplateChild(ElementPanel) as Panel;
			if (_panel != null) {
				if (_buttonOk != null) {
					_panel.Children.Add(_buttonOk);
				}

				if (_buttonYes != null) {
					_panel.Children.Add(_buttonYes);
				}

				if (_buttonNo != null) {
					_panel.Children.Add(_buttonNo);
				}

				if (_buttonCancel != null) {
					_panel.Children.Add(_buttonCancel);
				}
			}

			_buttonClose = GetTemplateChild(ElementButtonClose) as Button;
			if (_buttonClose != null) {
				_buttonClose.Click += ButtonClose_Click;
			}
		}

		private void ButtonClose_Click(object sender, RoutedEventArgs e) => Close();

		protected override void OnPreviewKeyDown(KeyEventArgs e) {
			if (e.Key == Key.System && e.SystemKey == Key.F4) {
				e.Handled = true;
				return;
			}

			if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.C) {
				var builder = new StringBuilder();
				var line = new string('-', 27);
				builder.Append(line);
				builder.Append(Environment.NewLine);
				builder.Append(Title);
				builder.Append(Environment.NewLine);
				builder.Append(line);
				builder.Append(Environment.NewLine);
				builder.Append(Message);
				builder.Append(Environment.NewLine);
				builder.Append(line);
				builder.Append(Environment.NewLine);
				if (_showOk) {
					builder.Append(OkButtonText);
					builder.Append("   ");
				}
				if (_showYes) {
					builder.Append(YesButtonText);
					builder.Append("   ");
				}
				if (_showNo) {
					builder.Append(NoButtonText);
					builder.Append("   ");
				}
				if (_showCancel) {
					builder.Append(CancelButtonText);
					builder.Append("   ");
				}
				builder.Append(Environment.NewLine);
				builder.Append(line);
				builder.Append(Environment.NewLine);

				try {
					Clipboard.SetDataObject(builder.ToString());
				} catch {
					// ignored
				}
			}
		}

		/// <summary>
		///     成功
		/// </summary>
		/// <param name="messageBoxText"></param>
		/// <param name="caption"></param>
		public static MessageBoxResult Success(string messageBoxText, string caption = null) {
			MessageBox messageBox = null;
			Application.Current.Dispatcher.Invoke(() => {
				messageBox = CreateMessageBox(null, messageBoxText, caption, MessageBoxResult.OK);
				SetButtonStatus(messageBox, MessageBoxButton.OK, MessageBoxResult.OK);
				messageBox.ShowImage = true;
				messageBox.Image = ResourceHelper.GetResourceInternal<Geometry>(ResourceToken.SuccessGeometry);
				messageBox.ImageBrush = ResourceHelper.GetResourceInternal<Brush>(ResourceToken.SuccessBrush);
				SystemSounds.Asterisk.Play();
				showCount++;
				messageBox.ShowDialog();
				showCount--;
			});

			return messageBox._messageBoxResult;
		}

		/// <summary>
		///     消息
		/// </summary>
		/// <param name="messageBoxText"></param>
		/// <param name="caption"></param>
		public static MessageBoxResult Info(string messageBoxText, string caption = null) {
			MessageBox messageBox = null;
			Application.Current.Dispatcher.Invoke(() => {
				messageBox = CreateMessageBox(null, messageBoxText, caption, MessageBoxResult.OK);
				SetButtonStatus(messageBox, MessageBoxButton.OK, MessageBoxResult.OK);
				SetImage(messageBox, MessageBoxImage.Information);
				SystemSounds.Asterisk.Play();
				showCount++;
				messageBox.ShowDialog();
				showCount--;
			});

			return messageBox._messageBoxResult;
		}

		/// <summary>
		///     警告
		/// </summary>
		/// <param name="messageBoxText"></param>
		/// <param name="caption"></param>
		public static MessageBoxResult Warning(string messageBoxText, string caption = null) {
			MessageBox messageBox = null;
			Application.Current.Dispatcher.Invoke(() => {
				messageBox = CreateMessageBox(null, messageBoxText, caption, MessageBoxResult.OK);
				SetButtonStatus(messageBox, MessageBoxButton.OK, MessageBoxResult.OK);
				SetImage(messageBox, MessageBoxImage.Warning);
				SystemSounds.Asterisk.Play();
				showCount++;
				messageBox.ShowDialog();
				showCount--;
			});

			return messageBox._messageBoxResult;
		}

		/// <summary>
		///     错误
		/// </summary>
		/// <param name="messageBoxText"></param>
		/// <param name="caption"></param>
		public static MessageBoxResult Error(string messageBoxText, string caption = null) {
			MessageBox messageBox = null;
			Application.Current.Dispatcher.Invoke(() => {
				messageBox = CreateMessageBox(null, messageBoxText, caption, MessageBoxResult.OK);
				SetButtonStatus(messageBox, MessageBoxButton.OK, MessageBoxResult.OK);
				SetImage(messageBox, MessageBoxImage.Error);
				SystemSounds.Asterisk.Play();
				showCount++;
				messageBox.ShowDialog();
				showCount--;
			});

			return messageBox._messageBoxResult;
		}

		/// <summary>
		///     严重
		/// </summary>
		/// <param name="messageBoxText"></param>
		/// <param name="caption"></param>
		public static MessageBoxResult Fatal(string messageBoxText, string caption = null) {
			MessageBox messageBox = null;
			Application.Current.Dispatcher.Invoke(() => {
				messageBox = CreateMessageBox(null, messageBoxText, caption, MessageBoxResult.OK);
				SetButtonStatus(messageBox, MessageBoxButton.OK, MessageBoxResult.OK);
				messageBox.ShowImage = true;
				messageBox.Image = ResourceHelper.GetResourceInternal<Geometry>(ResourceToken.FatalGeometry);
				messageBox.ImageBrush = ResourceHelper.GetResourceInternal<Brush>(ResourceToken.PrimaryTextBrush);
				SystemSounds.Asterisk.Play();
				showCount++;
				messageBox.ShowDialog();
				showCount--;
			});

			return messageBox._messageBoxResult;
		}

		/// <summary>
		///     询问，按钮为<see cref="MessageBoxButton.OKCancel"/>
		/// </summary>
		/// <param name="messageBoxText"></param>
		/// <param name="caption"></param>
		public static MessageBoxResult Ask(string messageBoxText, string caption = null) {
			MessageBox messageBox = null;
			Application.Current.Dispatcher.Invoke(() => {
				messageBox = CreateMessageBox(null, messageBoxText, caption, MessageBoxResult.Cancel);
				SetButtonStatus(messageBox, MessageBoxButton.OKCancel, MessageBoxResult.Cancel);
				SetImage(messageBox, MessageBoxImage.Question);
				SystemSounds.Asterisk.Play();
				showCount++;
				messageBox.ShowDialog();
				showCount--;
			});

			return messageBox._messageBoxResult;
		}

		/// <summary>
		///     自定义信息展示
		/// </summary>
		/// <param name="info"></param>
		/// <returns></returns>
		public static MessageBoxResult Show(MessageBoxInfo info) {
			MessageBox messageBox = null;
			Application.Current.Dispatcher.Invoke(() => {
				messageBox = CreateMessageBox(null, info.Message, info.Caption, info.DefaultResult);
				messageBox.OkButtonText = info.OkButtonText;
				messageBox.CancelButtonText = info.CancelButtonText;
				messageBox.YesButtonText = info.YesButtonText;
				messageBox.NoButtonText = info.NoButtonText;
				SetButtonStatus(messageBox, info.Button, info.DefaultResult);
				SetImage(messageBox, info.Image);

				if (info.StyleKey != null) {
					messageBox.Style = ResourceHelper.GetResource<Style>(info.StyleKey) ?? info.Style;
				}

				if (info.CheckBoxText != null) {
					messageBox.CheckBoxVisibility = Visibility.Visible;
					messageBox.CheckBoxText = info.CheckBoxText;
				}

				SystemSounds.Asterisk.Play();
				showCount++;
				messageBox.ShowDialog();
				showCount--;
				info.IsChecked = messageBox.IsChecked.GetValueOrDefault();
			});

			return messageBox._messageBoxResult;
		}

		/// <summary>
		///     信息展示
		/// </summary>
		/// <param name="messageBoxText"></param>
		/// <param name="caption"></param>
		/// <param name="button"></param>
		/// <param name="icon"></param>
		/// <param name="defaultResult"></param>
		/// <returns></returns>
		public static MessageBoxResult Show(string messageBoxText, string caption = null,
			MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None,
			MessageBoxResult defaultResult = MessageBoxResult.None) =>
			Show(null, messageBoxText, caption, button, icon, defaultResult);

		/// <summary>
		///     信息展示
		/// </summary>
		/// <param name="owner"></param>
		/// <param name="messageBoxText"></param>
		/// <param name="caption"></param>
		/// <param name="button"></param>
		/// <param name="icon"></param>
		/// <param name="defaultResult"></param>
		/// <returns></returns>
		public static MessageBoxResult Show(System.Windows.Window owner, string messageBoxText, string caption = null, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None) {
			MessageBox messageBox = null;
			Application.Current.Dispatcher.Invoke(() => {
				messageBox = CreateMessageBox(owner, messageBoxText, caption, defaultResult);
				SetButtonStatus(messageBox, button, defaultResult);
				SetImage(messageBox, icon);
				SystemSounds.Asterisk.Play();
				showCount++;
				messageBox.ShowDialog();
				showCount--;
			});

			return messageBox._messageBoxResult;
		}

		private static MessageBox CreateMessageBox(
			System.Windows.Window owner,
			string messageBoxText,
			string caption,
			MessageBoxResult defaultResult) {
			if (!IsValidMessageBoxResult(defaultResult)) {
				throw new InvalidEnumArgumentException(nameof(defaultResult), (int)defaultResult, typeof(MessageBoxResult));
			}

			var ownerWindow = owner ?? WindowHelper.GetActiveWindow();
			var ownerIsNull = ownerWindow is null;

			return new MessageBox {
				Message = messageBoxText,
				Owner = ownerWindow,
				WindowStartupLocation = WindowStartupLocation.CenterScreen,
				ShowTitle = true,
				Title = caption ?? string.Empty,
				Topmost = ownerIsNull,
				_messageBoxResult = defaultResult
			};
		}

		private static void SetButtonStatus(MessageBox messageBox, MessageBoxButton messageBoxButton, MessageBoxResult defaultResult) {
			switch (messageBoxButton) {
			case MessageBoxButton.OK:
				messageBox._showOk = true;
				messageBox._buttonOk = new Button {
					IsCancel = true,
					IsDefault = true,
					Content = messageBox.OkButtonText,
					Command = ControlCommands.Confirm,
					Style = ResourceHelper.GetResourceInternal<Style>("MessageBoxButtonStyle")
				};

				break;
			case MessageBoxButton.OKCancel:
				messageBox._showOk = true;
				messageBox._buttonOk = new Button {
					Content = messageBox.OkButtonText,
					Command = ControlCommands.Confirm
				};

				messageBox._showCancel = true;
				messageBox._buttonCancel = new Button {
					IsCancel = true,
					Content = messageBox.CancelButtonText,
					Command = ControlCommands.Cancel
				};

				if (defaultResult == MessageBoxResult.Cancel) {
					messageBox._buttonOk.Style = ResourceHelper.GetResourceInternal<Style>("MessageBoxButtonStyle");
					messageBox._buttonCancel.Style = ResourceHelper.GetResourceInternal<Style>("MessageBoxPrimaryButtonStyle");
					messageBox._buttonCancel.IsDefault = true;
				} else {
					messageBox._buttonOk.Style = ResourceHelper.GetResourceInternal<Style>("MessageBoxPrimaryButtonStyle");
					messageBox._buttonOk.IsDefault = true;
					messageBox._buttonCancel.Style = ResourceHelper.GetResourceInternal<Style>("MessageBoxButtonStyle");
				}

				break;
			case MessageBoxButton.YesNo:
				messageBox._showYes = true;
				messageBox._buttonYes = new Button {
					Content = messageBox.YesButtonText,
					Command = ControlCommands.Yes
				};

				messageBox._showNo = true;
				messageBox._buttonNo = new Button {
					Content = messageBox.NoButtonText,
					Command = ControlCommands.No
				};

				if (defaultResult == MessageBoxResult.No) {
					messageBox._buttonYes.Style = ResourceHelper.GetResourceInternal<Style>("MessageBoxButtonStyle");
					messageBox._buttonNo.Style = ResourceHelper.GetResourceInternal<Style>("MessageBoxPrimaryButtonStyle");
					messageBox._buttonNo.IsDefault = true;
				} else {
					messageBox._buttonYes.Style = ResourceHelper.GetResourceInternal<Style>("MessageBoxPrimaryButtonStyle");
					messageBox._buttonYes.IsDefault = true;
					messageBox._buttonNo.Style = ResourceHelper.GetResourceInternal<Style>("MessageBoxButtonStyle");
				}

				break;
			case MessageBoxButton.YesNoCancel:
				messageBox._showYes = true;
				messageBox._buttonYes = new Button {
					Content = messageBox.YesButtonText,
					Command = ControlCommands.Yes
				};

				messageBox._showNo = true;
				messageBox._buttonNo = new Button {
					Content = messageBox.NoButtonText,
					Command = ControlCommands.No
				};

				messageBox._showCancel = true;
				messageBox._buttonCancel = new Button {
					IsCancel = true,
					Content = messageBox.CancelButtonText,
					Command = ControlCommands.Cancel
				};

				switch (defaultResult) {
				case MessageBoxResult.No:
					messageBox._buttonYes.Style = ResourceHelper.GetResourceInternal<Style>("MessageBoxButtonStyle");
					messageBox._buttonNo.Style = ResourceHelper.GetResourceInternal<Style>("MessageBoxPrimaryButtonStyle");
					messageBox._buttonNo.IsDefault = true;
					messageBox._buttonCancel.Style = ResourceHelper.GetResourceInternal<Style>("MessageBoxButtonStyle");
					break;
				case MessageBoxResult.Cancel:
					messageBox._buttonYes.Style = ResourceHelper.GetResourceInternal<Style>("MessageBoxButtonStyle");
					messageBox._buttonNo.Style = ResourceHelper.GetResourceInternal<Style>("MessageBoxButtonStyle");
					messageBox._buttonCancel.Style = ResourceHelper.GetResourceInternal<Style>("MessageBoxPrimaryButtonStyle");
					messageBox._buttonCancel.IsDefault = true;
					break;
				default:
					messageBox._buttonYes.Style = ResourceHelper.GetResourceInternal<Style>("MessageBoxPrimaryButtonStyle");
					messageBox._buttonYes.IsDefault = true;
					messageBox._buttonNo.Style = ResourceHelper.GetResourceInternal<Style>("MessageBoxButtonStyle");
					messageBox._buttonCancel.Style = ResourceHelper.GetResourceInternal<Style>("MessageBoxButtonStyle");
					break;
				}

				break;
			}
		}

		private static void SetImage(MessageBox messageBox, MessageBoxImage messageBoxImage) {
			string iconKey, iconBrushKey;
			switch (messageBoxImage) {
			case MessageBoxImage.Error:
				iconKey = ResourceToken.ErrorGeometry;
				iconBrushKey = ResourceToken.DangerBrush;
				break;
			case MessageBoxImage.Question:
				iconKey = ResourceToken.AskGeometry;
				iconBrushKey = ResourceToken.InfoBrush;
				break;
			case MessageBoxImage.Warning:
				iconKey = ResourceToken.WarningGeometry;
				iconBrushKey = ResourceToken.WarningBrush;
				break;
			case MessageBoxImage.Information:
				iconKey = ResourceToken.InfoGeometry;
				iconBrushKey = ResourceToken.InfoBrush;
				break;
			default:
				return;
			}
			messageBox.ShowImage = true;
			messageBox.Image = ResourceHelper.GetResourceInternal<Geometry>(iconKey);
			messageBox.ImageBrush = ResourceHelper.GetResourceInternal<Brush>(iconBrushKey);
		}

		private static bool IsValidMessageBoxResult(MessageBoxResult value) {
			return value is MessageBoxResult.Cancel or MessageBoxResult.No or MessageBoxResult.None or MessageBoxResult.OK or MessageBoxResult.Yes;
		}
	}
}
