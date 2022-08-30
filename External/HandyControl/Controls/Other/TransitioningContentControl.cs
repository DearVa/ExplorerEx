using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using HandyControl.Data;
using HandyControl.Tools;

namespace HandyControl.Controls; 

public class TransitioningContentControl : ContentControl {
	private FrameworkElement contentPresenter;

	private static Storyboard storyboardBuildInDefault;

	private Storyboard storyboardBuildIn;

	public TransitioningContentControl() {
		Loaded += TransitioningContentControl_Loaded;
		Unloaded += TransitioningContentControl_Unloaded;
	}

	public static readonly DependencyProperty TransitionModeProperty = DependencyProperty.Register(
		nameof(TransitionMode), typeof(TransitionMode), typeof(TransitioningContentControl), new PropertyMetadata(default(TransitionMode), OnTransitionModeChanged));

	private static void OnTransitionModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var ctl = (TransitioningContentControl)d;
		ctl.OnTransitionModeChanged((TransitionMode)e.NewValue);
	}

	private void OnTransitionModeChanged(TransitionMode newValue) {
		storyboardBuildIn = ResourceHelper.GetResourceInternal<Storyboard>($"{newValue}Transition");
		StartTransition();
	}

	public TransitionMode TransitionMode {
		get => (TransitionMode)GetValue(TransitionModeProperty);
		set => SetValue(TransitionModeProperty, value);
	}

	public static readonly DependencyProperty TransitionStoryboardProperty = DependencyProperty.Register(
		nameof(TransitionStoryboard), typeof(Storyboard), typeof(TransitioningContentControl), new PropertyMetadata(default(Storyboard)));

	public Storyboard TransitionStoryboard {
		get => (Storyboard)GetValue(TransitionStoryboardProperty);
		set => SetValue(TransitionStoryboardProperty, value);
	}

	private void TransitioningContentControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => StartTransition();

	private void TransitioningContentControl_Loaded(object sender, RoutedEventArgs e) {
		IsVisibleChanged += TransitioningContentControl_IsVisibleChanged;
	}

	private void TransitioningContentControl_Unloaded(object sender, RoutedEventArgs e) {
		IsVisibleChanged -= TransitioningContentControl_IsVisibleChanged;
	}

	private void StartTransition() {
		if (!IsArrangeValid || contentPresenter == null) {
			return;
		}

		if (TransitionStoryboard != null) {
			TransitionStoryboard.Begin(contentPresenter);
		} else if (storyboardBuildIn != null) {
			storyboardBuildIn?.Begin(contentPresenter);
		} else {
			storyboardBuildInDefault ??= ResourceHelper.GetResourceInternal<Storyboard>($"{default(TransitionMode)}Transition");
			storyboardBuildInDefault?.Begin(contentPresenter);
		}
	}

	public override void OnApplyTemplate() {
		base.OnApplyTemplate();

		contentPresenter = VisualTreeHelper.GetChild(this, 0) as FrameworkElement;
		if (contentPresenter != null) {
			contentPresenter.RenderTransformOrigin = new Point(0.5, 0.5);
			contentPresenter.RenderTransform = new TransformGroup {
				Children = {
					new ScaleTransform(),
					new SkewTransform(),
					new RotateTransform(),
					new TranslateTransform()
				}
			};
		}
	}

	private bool rendered;

	protected override void OnRender(DrawingContext drawingContext) {
		base.OnRender(drawingContext);
		if (rendered) {
			return;
		}
		rendered = true;
		StartTransition();
	}
}