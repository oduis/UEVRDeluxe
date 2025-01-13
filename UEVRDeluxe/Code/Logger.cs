using Microsoft.Extensions.Logging;
using NReco.Logging.File;
using System;

namespace UEVRDeluxe.Code;

/// <summary>Very simple file logging</summary>
public static class Logger {
	public static ILogger Log { get; private set; }

	static LoggerFactory loggerFactory = new();

	public static void Startup() {
		var provider = new FileLoggerProvider(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\UEVRDeluxe\\Log.txt", false);
		provider.FormatLogEntry = (entry) => $"{DateTime.UtcNow:yy-MM-dd HH:mm:ss,fff} {entry.LogLevel}: {entry.Message}{(entry.Exception != null ? Environment.NewLine + entry.Exception : "")}";
		loggerFactory.AddProvider(provider);

		Log = loggerFactory.CreateLogger("APP");
	}

	public static void Shutdown() {
		loggerFactory.Dispose();  // To write missing entires in the queue
	}
}

