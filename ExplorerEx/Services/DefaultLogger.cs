using System;
using System.IO;
using System.Reflection;
using System.Xml;
using ExplorerEx.Assets;
using ExplorerEx.Definitions.Interfaces;
using NLog;
using NLog.Config;
using LogLevel = ExplorerEx.Definitions.Interfaces.LogLevel;

namespace ExplorerEx.Services;

public class DefaultLogger : ILoggerService {
	public delegate void LoggerOutputHandler(LogLevel level, object message);

	public delegate void LoggerExceptionOutputHandler(Exception exception);

	public static event LoggerOutputHandler? LogHandler;
	public static event LoggerExceptionOutputHandler? ExceptionHandler;

	private readonly Logger logger;

	public DefaultLogger() {
		System.Diagnostics.Debug.Assert(logger == null);
		try {
			GlobalDiagnosticsContext.Set("LogPath", Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Logs"));
			using var stream = new MemoryStream(Embedded.Config_Log);
			LogManager.Configuration = new XmlLoggingConfiguration(XmlReader.Create(stream));
			logger = LogManager.GetLogger("logger");
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_OnUnhandledException;
		} catch (Exception e) {
			Environment.Exit(e.HResult);
		}
	}

	private void CurrentDomain_OnUnhandledException(object sender, UnhandledExceptionEventArgs e) {
		if (e.ExceptionObject is Exception ex) {
			Exception(ex);
		} else {
			Error(Strings.Resources.UnknownError);
		}
	}

	public void Log(LogLevel level, object message, bool showOutput = false) {
		var nLevel = level switch {
			LogLevel.Debug => NLog.LogLevel.Debug,
			LogLevel.Info => NLog.LogLevel.Info,
			LogLevel.Warn => NLog.LogLevel.Warn,
			LogLevel.Error => NLog.LogLevel.Error,
			_ => NLog.LogLevel.Off
		};
		logger.Log(nLevel, message);
		if (showOutput) {
			LogHandler?.Invoke(level, message);
		}
	}

	public void Debug(object message, bool showOutput = false) {
		Log(LogLevel.Debug, message, showOutput);
	}

	public void Info(object message, bool showOutput = false) {
		Log(LogLevel.Info, message, showOutput);
	}

	public void Warn(object message, bool showOutput = false) {
		Log(LogLevel.Warn, message, showOutput);
	}

	public void Error(object message, bool showOutput = false) {
		Log(LogLevel.Error, message, showOutput);
	}

	public void Exception(Exception e, bool showOutput = false) {
		logger.Fatal(e);
		if (showOutput) {
			ExceptionHandler?.Invoke(e);
		}
	}
}
