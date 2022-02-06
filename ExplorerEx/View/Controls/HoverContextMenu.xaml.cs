using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using HandyControl.Tools;

namespace ExplorerEx.View.Controls;

/// <summary>
/// 一个悬浮的菜单（徒增功耗
/// </summary>
public partial class HoverContextMenu {
	private AxisAngleRotation3D axisAngleRotation3D;
	private Viewport2DVisual3D visual3D;
	private Border border;
	private double whRatio;  // 长宽比

	public HoverContextMenu() {
		InitializeComponent();
	}

	public override void OnApplyTemplate() {
		base.OnApplyTemplate();
		axisAngleRotation3D = (AxisAngleRotation3D)GetTemplateChild("AxisAngleRotation3D");
		border = (Border)GetTemplateChild("Border")!;
		border.MouseMove += OnMouseMove;
		border.MouseLeave += OnMouseLeave;
		visual3D = (Viewport2DVisual3D)GetTemplateChild("Viewport2DVisual3D");
	}

	protected override void OnOpened(RoutedEventArgs e) {
		var width = border.ActualWidth;
		Width = width * 1.2d;
		var height = border.ActualHeight;
		Height = height * 1.2d;
		visual3D.Geometry = new MeshGeometry3D {
			TriangleIndices = { 0, 1, 2, 0, 2, 3, },
			TextureCoordinates = new PointCollection {
				new(0, 0), new(0, 1), new(1, 1), new(1, 0)
			},
			Positions = new Point3DCollection {
				new(-width, height, 0), new(-width, -height, 0), new(width, -height, 0), new(width, height, 0)
			}
		};
		whRatio = width / height;
	}

	private void OnMouseMove(object sender, MouseEventArgs e) {
		CalculateAnimation(e, 25);
	}

	private void OnMouseLeave(object sender, MouseEventArgs e) {
		var da = new DoubleAnimation {
			Duration = new Duration(TimeSpan.FromSeconds(0.5d)),
			To = 0d
		};
		axisAngleRotation3D.BeginAnimation(AxisAngleRotation3D.AngleProperty, da);
	}

	protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e) {
		CalculateAnimation(e, 50);
		base.OnPreviewMouseLeftButtonDown(e);
	}

	protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e) {
		base.OnPreviewMouseLeftButtonUp(e);
		if (e.OriginalSource.IsChildOf(typeof(Button))) {
			IsOpen = false;
		}
	}

	private void CalculateAnimation(MouseEventArgs e, double pressForce) {
		var moveX = -(e.GetPosition(border).Y / border.ActualWidth - 0.5) * pressForce * whRatio;
		var moveY = (e.GetPosition(border).X / border.ActualHeight - 0.5) * pressForce;

		var da = new DoubleAnimation {
			Duration = new Duration(TimeSpan.FromSeconds(0.5d)),
			To = 10d
		};
		var axis = new Vector3D(moveX, moveY, 0);
		axisAngleRotation3D.Axis = axis;
		axisAngleRotation3D.BeginAnimation(AxisAngleRotation3D.AngleProperty, da);
	}
}