using System;

namespace ExplorerEx.Definitions.Interfaces;

public interface ILoggerService {
	void Log(LogLevel level, object message, bool showOutput = false);
	void Debug(object message, bool showOutput = false);
	void Info(object message, bool showOutput = false);
	void Warn(object message, bool showOutput = false);
	void Error(object message, bool showOutput = false);

	void Exception(Exception e, bool showOutput = true);
}

public enum LogLevel {
	Debug,
	Info,
	Warn,
	Error
}
