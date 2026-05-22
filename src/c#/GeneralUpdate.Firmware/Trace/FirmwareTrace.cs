using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace GeneralUpdate.Firmware.Trace
{
    /// <summary>
    /// Built-in trace logging for GeneralUpdate.Firmware.
    /// Wraps <see cref="System.Diagnostics.Trace"/> to provide structured,
    /// consistent logging throughout the firmware update lifecycle.
    /// 
    /// <para>
    /// All trace messages include timestamp, level, source, and context.
    /// Developers can attach custom <see cref="TraceListener"/> instances
    /// (such as <see cref="FirmwareTraceListener"/>) to integrate with their
    /// own logging infrastructure.
    /// </para>
    /// 
    /// <para>Usage:</para>
    /// <code>
    /// // Add the default firmware trace listener
    /// FirmwareTrace.Initialize();
    /// 
    /// // Or add a custom listener for integration with ILogger, NLog, etc.
    /// var listener = new FirmwareTraceListener();
    /// listener.OnTrace = (msg, level) => Console.WriteLine($"[{level}] {msg}");
    /// System.Diagnostics.Trace.Listeners.Add(listener);
    /// </code>
    /// </summary>
    public static class FirmwareTrace
    {
        private static readonly object SyncLock = new object();
        private static bool _initialized;

        /// <summary>
        /// Gets the trace source name used for all firmware trace messages.
        /// </summary>
        public const string SourceName = "GeneralUpdate.Firmware";

        /// <summary>
        /// Initializes the firmware trace system by adding the default
        /// <see cref="FirmwareTraceListener"/> to <see cref="System.Diagnostics.Trace.Listeners"/>.
        /// Call once at application startup. Subsequent calls are idempotent.
        /// </summary>
        public static void Initialize()
        {
            lock (SyncLock)
            {
                if (_initialized) return;

                // Add the default listener if none exists
                bool hasFirmwareListener = false;
                foreach (TraceListener listener in System.Diagnostics.Trace.Listeners)
                {
                    if (listener is FirmwareTraceListener)
                    {
                        hasFirmwareListener = true;
                        break;
                    }
                }

                if (!hasFirmwareListener)
                {
                    System.Diagnostics.Trace.Listeners.Add(new FirmwareTraceListener());
                }

                _initialized = true;
            }
        }

        /// <summary>
        /// Writes an informational trace message.
        /// Use for normal operation events such as configuration loaded, strategy selected,
        /// download started/completed, backup created, etc.
        /// </summary>
        /// <param name="message">The log message.</param>
        public static void Info(string message)
        {
            WriteLine(message, TraceLevel.Info);
        }

        /// <summary>
        /// Writes a formatted informational trace message.
        /// </summary>
        /// <param name="format">The composite format string.</param>
        /// <param name="args">An array of objects to write using format.</param>
        public static void Info(string format, params object[] args)
        {
            WriteLine(string.Format(CultureInfo.InvariantCulture, format, args), TraceLevel.Info);
        }

        /// <summary>
        /// Writes a warning trace message.
        /// Use for non-critical issues such as retry attempts, fallback paths,
        /// missing optional configuration, etc.
        /// </summary>
        /// <param name="message">The log message.</param>
        public static void Warn(string message)
        {
            WriteLine(message, TraceLevel.Warning);
        }

        /// <summary>
        /// Writes a formatted warning trace message.
        /// </summary>
        /// <param name="format">The composite format string.</param>
        /// <param name="args">An array of objects to write using format.</param>
        public static void Warn(string format, params object[] args)
        {
            WriteLine(string.Format(CultureInfo.InvariantCulture, format, args), TraceLevel.Warning);
        }

        /// <summary>
        /// Writes an error trace message.
        /// Use for failures such as download errors, validation failures,
        /// flashing errors, backup failures, etc.
        /// </summary>
        /// <param name="message">The log message.</param>
        public static void Error(string message)
        {
            WriteLine(message, TraceLevel.Error);
        }

        /// <summary>
        /// Writes a formatted error trace message.
        /// </summary>
        /// <param name="format">The composite format string.</param>
        /// <param name="args">An array of objects to write using format.</param>
        public static void Error(string format, params object[] args)
        {
            WriteLine(string.Format(CultureInfo.InvariantCulture, format, args), TraceLevel.Error);
        }

        /// <summary>
        /// Writes an error trace message with an associated exception.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="exception">The exception to log.</param>
        public static void Error(string message, Exception exception)
        {
            if (exception == null)
            {
                WriteLine(message, TraceLevel.Error);
                return;
            }

            string fullMessage = string.Format(
                CultureInfo.InvariantCulture,
                "{0} | Exception: {1} | StackTrace: {2}",
                message,
                exception.Message,
                exception.StackTrace ?? "(none)");
            WriteLine(fullMessage, TraceLevel.Error);
        }

        /// <summary>
        /// Writes a debug-level trace message.
        /// Use for detailed diagnostic information during development.
        /// These messages are typically suppressed in production.
        /// </summary>
        /// <param name="message">The log message.</param>
        public static void Debug(string message)
        {
            WriteLine(message, TraceLevel.Verbose);
        }

        /// <summary>
        /// Writes a formatted debug-level trace message.
        /// </summary>
        /// <param name="format">The composite format string.</param>
        /// <param name="args">An array of objects to write using format.</param>
        public static void Debug(string format, params object[] args)
        {
            WriteLine(string.Format(CultureInfo.InvariantCulture, format, args), TraceLevel.Verbose);
        }

        /// <summary>
        /// Writes a trace message indicating the start of a named operation.
        /// </summary>
        /// <param name="operationName">The name of the operation (e.g., "FirmwareDownload", "DeviceValidation").</param>
        public static void BeginOperation(string operationName)
        {
            Info("Begin operation: {0}", operationName ?? "(unknown)");
        }

        /// <summary>
        /// Writes a trace message indicating the completion of a named operation
        /// with the elapsed duration.
        /// </summary>
        /// <param name="operationName">The name of the operation.</param>
        /// <param name="elapsed">The elapsed time.</param>
        /// <param name="success">Whether the operation succeeded.</param>
        public static void EndOperation(string operationName, TimeSpan elapsed, bool success)
        {
            string status = success ? "SUCCESS" : "FAILED";
            Info(
                "End operation: {0} | Status={1} | Duration={2:F3}s",
                operationName ?? "(unknown)",
                status,
                elapsed.TotalSeconds);
        }

        /// <summary>
        /// Writes a trace message for a progress update.
        /// </summary>
        /// <param name="stage">The current stage description.</param>
        /// <param name="current">The current progress value.</param>
        /// <param name="total">The total expected value.</param>
        public static void Progress(string stage, long current, long total)
        {
            double percentage = total > 0 ? (double)current / total * 100.0 : 0.0;
            Info(
                "Progress [{0}]: {1}/{2} ({3:F1}%)",
                stage ?? "(unknown)",
                current,
                total,
                percentage);
        }

        /// <summary>
        /// Internal helper that writes a message to all trace listeners with the given level.
        /// </summary>
        /// <param name="message">The message to write.</param>
        /// <param name="level">The trace level.</param>
        private static void WriteLine(string message, TraceLevel level)
        {
            if (message == null) return;

            // Write to all listeners, respecting their filter settings
            foreach (TraceListener listener in System.Diagnostics.Trace.Listeners)
            {
                if (listener is FirmwareTraceListener ftl)
                {
                    ftl.WriteLine(message, level);
                }
                else
                {
                    // For non-FirmwareTraceListener instances, write formatted
                    try
                    {
                        string formatted = FirmwareTraceListener.FormatMessage(message, level);
                        // Only write if the listener's filter allows this level
                        TraceEventType eventType = TraceLevelToEventType(level);
                        if (listener.Filter == null || listener.Filter.ShouldTrace(
                            null, SourceName, eventType, 0, formatted, null, null, null))
                        {
                            listener.WriteLine(formatted);
                            listener.Flush();
                        }
                    }
                    catch (ThreadAbortException)
                    {
                        // Re-throw ThreadAbortException to avoid silent failures
                        throw;
                    }
                    catch (Exception)
                    {
                        // Silently ignore listener errors to avoid disrupting the update flow
                    }
                }
            }
        }

        /// <summary>
        /// Converts a <see cref="TraceLevel"/> to a <see cref="TraceEventType"/>.
        /// </summary>
        private static TraceEventType TraceLevelToEventType(TraceLevel level)
        {
            switch (level)
            {
                case TraceLevel.Error:
                    return TraceEventType.Error;
                case TraceLevel.Warning:
                    return TraceEventType.Warning;
                case TraceLevel.Info:
                    return TraceEventType.Information;
                case TraceLevel.Verbose:
                    return TraceEventType.Verbose;
                default:
                    return TraceEventType.Information;
            }
        }
    }
}
