#nullable enable

using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Threading;
using ExplorerEx.Utils;

namespace ExplorerEx.View.Controls; 

/// <summary>
/// 类似UWP的全窗口弹窗
/// </summary>
public partial class ContentDialog {
	public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
		"Title", typeof(string), typeof(ContentDialog), new PropertyMetadata(default(string)));

	public string? Title {
		get => (string)GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
		"Content", typeof(object), typeof(ContentDialog), new PropertyMetadata(default(object)));

	public object Content {
		get => GetValue(ContentProperty);
		set => SetValue(ContentProperty, value);
	}

	public static readonly DependencyProperty PrimaryButtonTextProperty = DependencyProperty.Register(
		"PrimaryButtonText", typeof(string), typeof(ContentDialog), new PropertyMetadata(default(string), PrimaryButtonText_OnChanged));

	private static void PrimaryButtonText_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var cd = (ContentDialog)d;
		if (e.NewValue == null && cd.SecondaryButtonText == null && cd.CancelButtonText == null) {
			cd.PrimaryButtonText = "Ok".L();
		}
	}

	public string? PrimaryButtonText {
		get => (string)GetValue(PrimaryButtonTextProperty);
		set => SetValue(PrimaryButtonTextProperty, value);
	}

	public static readonly DependencyProperty PrimaryButtonCommandProperty = DependencyProperty.Register(
		"PrimaryButtonCommand", typeof(ICommand), typeof(ContentDialog), new PropertyMetadata(default(ICommand)));

	public ICommand? PrimaryButtonCommand {
		get => (ICommand)GetValue(PrimaryButtonCommandProperty);
		set => SetValue(PrimaryButtonCommandProperty, value);
	}

	public static readonly DependencyProperty IsPrimaryButtonEnabledProperty = DependencyProperty.Register(
		"IsPrimaryButtonEnabled", typeof(bool), typeof(ContentDialog), new PropertyMetadata(true));

	public bool IsPrimaryButtonEnabled {
		get => (bool)GetValue(IsPrimaryButtonEnabledProperty);
		set => SetValue(IsPrimaryButtonEnabledProperty, value);
	}

	public static readonly DependencyProperty SecondaryButtonTextProperty = DependencyProperty.Register(
		"SecondaryButtonText", typeof(string), typeof(ContentDialog), new PropertyMetadata(default(string), SecondaryButtonText_OnChanged));

	private static void SecondaryButtonText_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var cd = (ContentDialog)d;
		cd.SecondaryButton.Margin = e.NewValue == null ? new Thickness() : new Thickness(0, 0, 10, 0);
	}

	public string? SecondaryButtonText {
		get => (string)GetValue(SecondaryButtonTextProperty);
		set => SetValue(SecondaryButtonTextProperty, value);
	}

	public static readonly DependencyProperty SecondaryButtonCommandProperty = DependencyProperty.Register(
		"SecondaryButtonCommand", typeof(ICommand), typeof(ContentDialog), new PropertyMetadata(default(ICommand)));

	public ICommand? SecondaryButtonCommand {
		get => (ICommand)GetValue(SecondaryButtonCommandProperty);
		set => SetValue(SecondaryButtonCommandProperty, value);
	}

	public static readonly DependencyProperty CancelButtonTextProperty = DependencyProperty.Register(
		"CancelButtonText", typeof(string), typeof(ContentDialog), new PropertyMetadata(default(string), CancelButtonText_OnChanged));

	private static void CancelButtonText_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var cd = (ContentDialog)d;
		cd.PrimaryButton.Margin = e.NewValue == null ? new Thickness() : new Thickness(0, 0, 10, 0);
	}

	public string? CancelButtonText {
		get => (string?)GetValue(CancelButtonTextProperty);
		set => SetValue(CancelButtonTextProperty, value);
	}

	public static readonly DependencyProperty CancelButtonCommandProperty = DependencyProperty.Register(
		"CancelButtonCommand", typeof(ICommand), typeof(ContentDialog), new PropertyMetadata(default(ICommand)));

	public ICommand? CancelButtonCommand {
		get => (ICommand)GetValue(CancelButtonCommandProperty);
		set => SetValue(CancelButtonCommandProperty, value);
	}

	public event Action? Shown; 

	private MainWindow? owner;
	private readonly DispatcherFrame dispatcherFrame;
	private ContentDialogResult result;

	private readonly DoubleAnimation opacityInAnimation, opacityOutAnimation, scaleInAnimation, scaleXOutAnimation, scaleYOutAnimation;

	public ContentDialog() {
		dispatcherFrame = new DispatcherFrame();

		var cubicEase = new CubicEase {
			EasingMode = EasingMode.EaseOut
		};
		opacityInAnimation = new DoubleAnimation(1d, TimeSpan.FromMilliseconds(300d)) {
			EasingFunction = cubicEase
		};
		opacityOutAnimation = new DoubleAnimation(0d, TimeSpan.FromMilliseconds(300d)) {
			EasingFunction = cubicEase
		};
		scaleInAnimation = new DoubleAnimation(0.8d, 1d, TimeSpan.FromMilliseconds(300d)) {
			EasingFunction = new BackEase {
				EasingMode = EasingMode.EaseOut,
				Amplitude = 1.2d
			}
		};
		scaleInAnimation.Completed += (_, _) => {
			ContentPresenter.Focus();
			Shown?.Invoke();
		};
		scaleXOutAnimation = new DoubleAnimation(1d, 1.2d, TimeSpan.FromMilliseconds(300d)) {
			EasingFunction = cubicEase
		};
		scaleYOutAnimation = new DoubleAnimation(1d, 1.2d, TimeSpan.FromMilliseconds(300d)) {
			EasingFunction = cubicEase
		};
		scaleYOutAnimation.Completed += (_, _) => {
			dispatcherFrame.Continue = false;
			owner!.RootPanel.Children.Remove(this);
			owner = null;
		};

		if (PrimaryButtonText == null && SecondaryButtonText == null && CancelButtonText == null) {
			PrimaryButtonText = "Ok".L();
		}
		if (CancelButtonText != null) {
			SecondaryButton.Margin = new Thickness(0, 0, 10, 0);
		}
		if (SecondaryButtonText != null || CancelButtonText != null) {
			PrimaryButton.Margin = new Thickness(0, 0, 10, 0);
		}

		DataContext = this;
		InitializeComponent();
	}

	private void ContentDialogPrimaryButton_OnClick(object sender, RoutedEventArgs e) {
		result = ContentDialogResult.Primary;
		Close();
	}

	private void ContentDialogSecondaryButton_OnClick(object sender, RoutedEventArgs e) {
		result = ContentDialogResult.Secondary;
		Close();
	}

	private void ContentDialogCancelButton_OnClick(object sender, RoutedEventArgs e) {
		result = ContentDialogResult.Cancel;
		Close();
	}

	public ContentDialogResult Show(MainWindow owner) {
		this.owner = owner;
		owner.RootPanel.Children.Add(this);
		BeginAnimation(OpacityProperty, opacityInAnimation);
		ScaleTf.BeginAnimation(ScaleTransform.ScaleXProperty, scaleInAnimation);
		ScaleTf.BeginAnimation(ScaleTransform.ScaleYProperty, scaleInAnimation);
		dispatcherFrame.Continue = true;
		Dispatcher.PushFrame(dispatcherFrame);
		return result;
	}

	public ContentDialogResult Show() {
		var owner = MainWindow.FocusedWindow;
		if (owner == null) {
			throw new ArgumentNullException();
		}
		return Show(owner);
	}

	public void Close() {
		BeginAnimation(OpacityProperty, opacityOutAnimation);
		ScaleTf.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXOutAnimation);
		ScaleTf.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYOutAnimation);
	}

	protected override void OnKeyDown(KeyEventArgs e) {
		base.OnKeyDown(e);
		switch (e.Key) {
		case Key.Enter:
			if (PrimaryButtonText != null) {
				result = ContentDialogResult.Primary;
				Close();
			}
			break;
		case Key.Escape:
			if (CancelButtonText != null) {
				result = ContentDialogResult.Cancel;
				Close();
			}
			break;
		case Key.C when e.KeyboardDevice.Modifiers == ModifierKeys.Control:
			if (Content is string s) {
				Clipboard.SetText(s);
			}
			break;
		}
	}

	public enum ContentDialogResult {
		Primary,
		Secondary,
		Cancel
	}
}