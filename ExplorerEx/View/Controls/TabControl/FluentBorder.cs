using System;
using System.Windows;
using System.Windows.Controls;

namespace ExplorerEx.View.Controls;

public class FluentBorder : Border {
	public static readonly DependencyProperty TopProperty = DependencyProperty.Register(
		"Top", typeof(double), typeof(FluentBorder), new PropertyMetadata(0d, TopProperty_OnChanged));

	private static void TopProperty_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var fb = (FluentBorder)d;
		var margin = fb.Margin;
		var top = (double)e.NewValue;
		fb.SetValue(MarginProperty, new Thickness(margin.Left, top, margin.Right, margin.Bottom));
		fb.SetValue(HeightProperty, Math.Max(fb.Bottom - top, 0d));
	}

	public double Top {
		get => (double)GetValue(TopProperty);
		set => SetValue(TopProperty, value);
	}

	public static readonly DependencyProperty BottomProperty = DependencyProperty.Register(
		"Bottom", typeof(double), typeof(FluentBorder), new PropertyMetadata(0d, BottomProperty_OnChanged));

	private static void BottomProperty_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var fb = (FluentBorder)d;
		fb.SetValue(HeightProperty, Math.Max((double)e.NewValue - fb.Margin.Top, 0d));
	}

	public double Bottom {
		get => (double)GetValue(BottomProperty);
		set => SetValue(BottomProperty, value);
	}
}