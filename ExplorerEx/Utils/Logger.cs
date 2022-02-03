using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Xml;
using NLog;
using NLog.Config;

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
			MessageBox.Show("未能初始化Logger\n" + e, "Fatal", MessageBoxButton.OK, MessageBoxImage.Stop);
		}
	}

	private static void CurrentDomain_OnUnhandledException(object sender, UnhandledExceptionEventArgs e) {
		if (e.ExceptionObject is Exception ex) {
			Exception(ex);
		} else {
			Error("Unknown_error".L());
		}
	}

	public static void Info(string msg, bool showMsgBox = false) {
		logger.Info(msg);
		if (showMsgBox) {
			MessageBox.Show(msg, "Info", MessageBoxButton.OK, MessageBoxImage.Information);
		}
	}

	public static void Debug(string msg, bool showMsgBox = false) {
		logger.Debug(msg);
		if (showMsgBox) {
			MessageBox.Show(msg, "Debug", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
	}

	public static void Error(string msg, bool showMsgBox = true) {
		logger.Error(msg);
		if (showMsgBox) {
			MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	public static void Exception(Exception e, bool showMsgBox = true) {
		logger.Fatal(e);
		if (showMsgBox) {
			MessageBox.Show(e.ToString(), "Fatal", MessageBoxButton.OK, MessageBoxImage.Stop);
		}
	}
}