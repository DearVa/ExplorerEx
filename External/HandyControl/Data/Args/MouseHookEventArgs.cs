﻿using System;
using HandyControl.Tools.Interop;

namespace HandyControl.Data; 

public class MouseHookEventArgs : EventArgs {
	public MouseHookMessageType MessageType { get; set; }

	public InteropValues.POINT Point { get; set; }
}