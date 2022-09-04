using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using HandyControl.Data;
using HandyControl.Tools;
using HandyControl.Tools.Interop;

namespace HandyControl.Controls; 

public class NotifyIcon : FrameworkElement, IDisposable {
	private bool added;

	private readonly object syncObj = new();

	private readonly int id;

	private ImageSource icon;

	private IntPtr iconCurrentHandle;

	private IntPtr iconDefaultHandle;

	private IconHandle iconHandle;

	private const int WmTrayMouseMessage = InteropValues.WM_USER + 1024;

	private string windowClassName;

	private int wmTaskbarCreated;

	public IntPtr Hwnd { get; private set; }

	private readonly InteropValues.WndProc callback;

	private Popup contextContent;

	private bool doubleClick;

	private bool isDisposed;

	private static int nextId;

	private static readonly Dictionary<string, NotifyIcon> NotifyIconDic = new();

	static NotifyIcon() {
		VisibilityProperty.OverrideMetadata(typeof(NotifyIcon), new PropertyMetadata(Visibility.Visible, OnVisibilityChanged));
		DataContextProperty.OverrideMetadata(typeof(NotifyIcon), new FrameworkPropertyMetadata(DataContextPropertyChanged));
		ContextMenuProperty.OverrideMetadata(typeof(NotifyIcon), new FrameworkPropertyMetadata(ContextMenuPropertyChanged));
	}

	private static void OnVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var ctl = (NotifyIcon)d;
		var v = (Visibility)e.NewValue;

		if (v == Visibility.Visible) {
			if (ctl.iconCurrentHandle == IntPtr.Zero) {
				ctl.OnIconChanged();
			}
			ctl.UpdateIcon(true);
		} else if (ctl.iconCurrentHandle != IntPtr.Zero) {
			ctl.UpdateIcon(false);
		}
	}

	private static void DataContextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
		((NotifyIcon)d).OnDataContextPropertyChanged(e);

	private void OnDataContextPropertyChanged(DependencyPropertyChangedEventArgs e) {
		UpdateDataContext(contextContent, e.OldValue, e.NewValue);
		UpdateDataContext(ContextMenu, e.OldValue, e.NewValue);
	}

	private static void ContextMenuPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var ctl = (NotifyIcon)d;
		ctl.OnContextMenuPropertyChanged(e);
	}

	private void OnContextMenuPropertyChanged(DependencyPropertyChangedEventArgs e) =>
		UpdateDataContext((ContextMenu)e.NewValue, null, DataContext);

	public NotifyIcon() {
		id = ++nextId;
		callback = Callback;

		Loaded += (_, _) => Init();

		if (Application.Current != null) {
			Application.Current.Exit += (_, _) => Dispose();
		}
	}

	~NotifyIcon() => Dispose(false);

	public void Init() {
		RegisterClass();
		if (Visibility == Visibility.Visible) {
			OnIconChanged();
			UpdateIcon(true);
		}
	}

	public static void Register(string token, NotifyIcon notifyIcon) {
		if (string.IsNullOrEmpty(token) || notifyIcon == null) {
			return;
		}
		NotifyIconDic[token] = notifyIcon;
	}

	public static void Unregister(string token, NotifyIcon notifyIcon) {
		if (string.IsNullOrEmpty(token) || notifyIcon == null) {
			return;
		}

		if (NotifyIconDic.ContainsKey(token)) {
			if (ReferenceEquals(NotifyIconDic[token], notifyIcon)) {
				NotifyIconDic.Remove(token);
			}
		}
	}

	public static void Unregister(NotifyIcon notifyIcon) {
		if (notifyIcon == null) {
			return;
		}
		var (key, _) = NotifyIconDic.FirstOrDefault(item => ReferenceEquals(notifyIcon, item.Value));
		if (!string.IsNullOrEmpty(key)) {
			NotifyIconDic.Remove(key);
		}
	}

	public static void Unregister(string token) {
		if (string.IsNullOrEmpty(token)) {
			return;
		}

		if (NotifyIconDic.ContainsKey(token)) {
			NotifyIconDic.Remove(token);
		}
	}

	public static void ShowBalloonTip(string title, string content, NotifyIconInfoType infoType, string token) {
		if (NotifyIconDic.TryGetValue(token, out var notifyIcon)) {
			notifyIcon.ShowBalloonTip(title, content, infoType);
		}
	}

	public void ShowBalloonTip(string title, string content, NotifyIconInfoType infoType) {
		if (!added || DesignerHelper.IsInDesignMode) {
			return;
		}

		var data = new InteropValues.NOTIFYICONDATA {
			uFlags = InteropValues.NIF_INFO,
			hWnd = Hwnd,
			uID = id,
			szInfoTitle = title ?? string.Empty,
			szInfo = content ?? string.Empty
		};

		data.dwInfoFlags = infoType switch {
			NotifyIconInfoType.Info => InteropValues.NIIF_INFO,
			NotifyIconInfoType.Warning => InteropValues.NIIF_WARNING,
			NotifyIconInfoType.Error => InteropValues.NIIF_ERROR,
			NotifyIconInfoType.None => InteropValues.NIIF_NONE,
			_ => data.dwInfoFlags
		};

		InteropMethods.Shell_NotifyIcon(InteropValues.NIM_MODIFY, data);
	}

	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	public void CloseContextControl() {
		if (contextContent != null) {
			contextContent.IsOpen = false;
		} else if (ContextMenu != null) {
			ContextMenu.IsOpen = false;
		}
	}

	public static readonly DependencyProperty TokenProperty = DependencyProperty.Register(
		nameof(Token), typeof(string), typeof(NotifyIcon), new PropertyMetadata(default(string), OnTokenChanged));

	private static void OnTokenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		if (d is NotifyIcon notifyIcon) {
			if (e.NewValue == null) {
				Unregister(notifyIcon);
			} else {
				Register(e.NewValue.ToString(), notifyIcon);
			}
		}
	}

	public string Token {
		get => (string)GetValue(TokenProperty);
		set => SetValue(TokenProperty, value);
	}

	public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
		nameof(Text), typeof(string), typeof(NotifyIcon), new PropertyMetadata(default(string)));

	public string Text {
		get => (string)GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
		nameof(Icon), typeof(ImageSource), typeof(NotifyIcon), new PropertyMetadata(default(ImageSource), OnIconChanged));

	private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var ctl = (NotifyIcon)d;
		ctl.icon = (ImageSource)e.NewValue;
		ctl.OnIconChanged();

		if (!string.IsNullOrEmpty(ctl.windowClassName) && ctl.Visibility == Visibility.Visible) {
			ctl.UpdateIcon(true);
		}
	}

	public ImageSource Icon {
		get => (ImageSource)GetValue(IconProperty);
		set => SetValue(IconProperty, value);
	}

	public static readonly DependencyProperty ContextContentProperty = DependencyProperty.Register(
		nameof(ContextContent), typeof(object), typeof(NotifyIcon), new PropertyMetadata(default(object)));

	public object ContextContent {
		get => GetValue(ContextContentProperty);
		set => SetValue(ContextContentProperty, value);
	}

	private void OnIconChanged() {
		if (windowClassName == null) {
			return;
		}

		if (icon != null) {
			IconHelper.GetIconHandlesFromImageSource(icon, out _, out iconHandle);
			iconCurrentHandle = iconHandle.CriticalGetHandle();
		} else {
			if (iconDefaultHandle == IntPtr.Zero) {
				IconHelper.GetDefaultIconHandles(out _, out iconHandle);
				iconDefaultHandle = iconHandle.CriticalGetHandle();
			}
			iconCurrentHandle = iconDefaultHandle;
		}
	}

	private void UpdateIcon(bool showIconInTray, bool isTransparent = false) {
		lock (syncObj) {
			if (DesignerHelper.IsInDesignMode) {
				return;
			}

			var data = new InteropValues.NOTIFYICONDATA {
				uCallbackMessage = WmTrayMouseMessage,
				uFlags = InteropValues.NIF_MESSAGE | InteropValues.NIF_ICON | InteropValues.NIF_TIP,
				hWnd = Hwnd,
				uID = id,
				dwInfoFlags = InteropValues.NIF_TIP,
				hIcon = isTransparent ? IntPtr.Zero : iconCurrentHandle,
				szTip = Text
			};

			if (showIconInTray) {
				if (!added) {
					InteropMethods.Shell_NotifyIcon(InteropValues.NIM_ADD, data);
					added = true;
				} else {
					InteropMethods.Shell_NotifyIcon(InteropValues.NIM_MODIFY, data);
				}
			} else if (added) {
				InteropMethods.Shell_NotifyIcon(InteropValues.NIM_DELETE, data);
				added = false;
			}
		}
	}

	private void RegisterClass() {
		windowClassName = $"ExplorerEx.NotifyIcon.{Guid.NewGuid()}";
		var wndClass = new InteropValues.WNDCLASS4ICON {
			style = 0,
			lpfnWndProc = callback,
			cbClsExtra = 0,
			cbWndExtra = 0,
			hInstance = IntPtr.Zero,
			hIcon = IntPtr.Zero,
			hCursor = IntPtr.Zero,
			hbrBackground = IntPtr.Zero,
			lpszMenuName = "",
			lpszClassName = windowClassName
		};

		InteropMethods.RegisterClass(wndClass);
		wmTaskbarCreated = InteropMethods.RegisterWindowMessage("TaskbarCreated");
		Hwnd = InteropMethods.CreateWindowEx(0, windowClassName, "", 0, 0, 0, 1, 1,
			IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
	}

	private IntPtr Callback(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam) {
		if (msg == wmTaskbarCreated) {
			if (Hwnd == hWnd && Visibility == Visibility.Visible) {
				added = false;
				UpdateIcon(true);
			}
		} else {
			switch (lParam.ToInt64()) {
			case InteropValues.WM_LBUTTONDBLCLK:
				WmMouseDown(MouseButton.Left, 2);
				break;
			case InteropValues.WM_LBUTTONUP:
				WmMouseUp(MouseButton.Left);
				break;
			case InteropValues.WM_RBUTTONUP:
				ShowContextMenu();
				WmMouseUp(MouseButton.Right);
				break;
			}
		}

		WndProc?.Invoke(hWnd, msg, wParam, lParam);

		return InteropMethods.DefWindowProc(hWnd, msg, wParam, lParam);
	}

	private void WmMouseDown(MouseButton button, int clicks) {
		if (clicks == 2) {
			RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, button) {
				RoutedEvent = MouseDoubleClickEvent
			});
			doubleClick = true;
		}
	}

	private void WmMouseUp(MouseButton button) {
		if (!doubleClick && button == MouseButton.Left) {
			RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, button) {
				RoutedEvent = ClickEvent
			});
		}
		doubleClick = false;
	}

	private void ShowContextMenu() {
		if (ContextContent != null) {
			if (contextContent == null) {
				contextContent = new Popup {
					Placement = PlacementMode.Mouse,
					AllowsTransparency = true,
					StaysOpen = false,
					UseLayoutRounding = true,
					SnapsToDevicePixels = true
				};
				contextContent.SetValue(BlurPopup.EnabledProperty, true);
			}

			contextContent.Child = new ContentControl {
				Content = ContextContent
			};
			contextContent.IsOpen = true;
			InteropMethods.SetForegroundWindow(contextContent.Child.GetHandle());
		} else if (ContextMenu != null) {
			if (ContextMenu.Items.Count == 0) {
				return;
			}

			ContextMenu.InvalidateProperty(StyleProperty);
			foreach (var item in ContextMenu.Items) {
				if (item is MenuItem menuItem) {
					menuItem.InvalidateProperty(StyleProperty);
				} else {
					var container = ContextMenu.ItemContainerGenerator.ContainerFromItem(item) as MenuItem;
					container?.InvalidateProperty(StyleProperty);
				}
			}

			ContextMenu.Placement = PlacementMode.Mouse;
			ContextMenu.IsOpen = true;

			InteropMethods.SetForegroundWindow(ContextMenu.GetHandle());
		}
	}

	public static readonly RoutedEvent ClickEvent =
		EventManager.RegisterRoutedEvent("Click", RoutingStrategy.Bubble,
			typeof(RoutedEventHandler), typeof(NotifyIcon));

	public event RoutedEventHandler Click {
		add => AddHandler(ClickEvent, value);
		remove => RemoveHandler(ClickEvent, value);
	}

	public static readonly RoutedEvent MouseDoubleClickEvent =
		EventManager.RegisterRoutedEvent("MouseDoubleClick", RoutingStrategy.Bubble,
			typeof(RoutedEventHandler), typeof(NotifyIcon));

	public event RoutedEventHandler MouseDoubleClick {
		add => AddHandler(MouseDoubleClickEvent, value);
		remove => RemoveHandler(MouseDoubleClickEvent, value);
}

	public event Action<IntPtr, int, IntPtr, IntPtr> WndProc;

	private void UpdateDataContext(FrameworkElement target, object oldValue, object newValue) {
		if (target == null || BindingOperations.GetBindingExpression(target, DataContextProperty) != null) {
			return;
		}
		if (ReferenceEquals(this, target.DataContext) || Equals(oldValue, target.DataContext)) {
			target.DataContext = newValue ?? this;
		}
	}

	private void Dispose(bool disposing) {
		if (isDisposed) {
			return;
		}
		if (disposing) {
			UpdateIcon(false);
		}

		isDisposed = true;
	}
}