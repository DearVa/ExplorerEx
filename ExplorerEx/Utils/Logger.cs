using System;
using System.IO;
using System.Reflection;
using System.Xml;
using NLog;
using NLog.Config;
using static ExplorerEx.Win32.Win32Interop;

namespace ExplorerEx.Utils; 

public static class Logger {
	private static NLog.Logger logger;

	public static void Initialize() {
		System.Diagnostics.Debug.Assert(logger == null);
		try {
			GlobalDiagnosticsContext.Set("LogPath", Path.Combine(Environment.CurrentDirectory, "Logs"));
			using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ExplorerEx.Assets.LogConfig.xml");
			LogManager.Configuration = new XmlLoggingConfiguration(XmlReader.Create(stream!));
			logger = LogManager.GetLogger("logger");
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_OnUnhandledException;
		} catch (Exception e) {
			MessageBox(IntPtr.Zero, "未能初始化Logger\n" + e, "Fatal", MessageBoxType.IconStop);
		}
	}

	private static void CurrentDomain_OnUnhandledException(object sender, UnhandledExceptionEventArgs e) {
		if (e.ExceptionObject is Exception ex) {
			Exception(ex);
		} else {
			Error("UnknownError".L());
		}
	}

	public static void Info(string msg, bool showMsgBox = false) {
		logger.Info(msg);
		if (showMsgBox) {
			MessageBox(IntPtr.Zero, msg, "Info", MessageBoxType.IconInformation);
		}
	}

	public static void Debug(string msg, bool showMsgBox = false) {
		logger.Debug(msg);
		if (showMsgBox) {
			MessageBox(IntPtr.Zero, msg, "Debug", MessageBoxType.IconAsterisk);
		}
	}

	public static void Error(string msg, bool showMsgBox = true) {
		logger.Error(msg);
		if (showMsgBox) {
			MessageBox(IntPtr.Zero, msg, "Error", MessageBoxType.IconError);
		}
	}

	public static void Exception(Exception e, bool showMsgBox = true) {
		logger.Fatal(e);
		if (showMsgBox) {
			MessageBox(IntPtr.Zero, e.ToString(), "Fatal", MessageBoxType.IconStop);
		}
	}
}