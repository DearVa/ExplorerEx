using System.Windows;

namespace HandyControl.Controls; 

public class ProgressBarAttach {
	public static readonly DependencyProperty AnimationDisabledProperty = DependencyProperty.RegisterAttached(
		"AnimationDisabled", typeof(bool), typeof(ProgressBarAttach), new PropertyMetadata(false));

	public static void SetAnimationDisabled(DependencyObject element, bool value)
		=> element.SetValue(AnimationDisabledProperty, value);

	public static bool GetAnimationDisabled(DependencyObject element)
		=> (bool)element.GetValue(AnimationDisabledProperty);
}