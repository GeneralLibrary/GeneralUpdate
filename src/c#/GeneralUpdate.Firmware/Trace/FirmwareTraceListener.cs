using System;
using System.Diagnostics;
using System.Globalization;

namespace GeneralUpdate.Firmware.Trace
{
    /// <summary>
    /// Default trace listener for GeneralUpdate.Firmware.
    /// Writes trace messages to the attached debug output and provides
    /// a hook for developers to inject custom listeners into their logging infrastructure.
    /// 
    /// <para>
    /// Developers can add this listener to integrate firmware update traces
    /// into their existing logging pipeline:
    /// </para>
    /// <code>
    /// System.Diagnostics.Trace.Listeners.Add(new FirmwareTraceListener());
    /// </code>
    /// </summary>
    public class FirmwareTraceListener : TraceListener
    {
        private const string SourceName = "GeneralUpdate.Firmware";

        /// <summary>
        /// Gets or sets a custom action that receives formatted trace messages.
        /// Set this to redirect trace output to a custom logger (e.g., ILogger, log4net, NLog, Serilog).
        /// When set, the default debug output is still emitted unless <see cref="SuppressDebugOutput"/> is true.
        /// </summary>
        public Action<string, TraceLevel> OnTrace { get; set; }

        /// <summary>
        /// Gets or sets whether to suppress the default Debug.WriteLine output.
        /// Set to true when using <see cref="OnTrace"/> to avoid duplicate output.
        /// Default value is false.
        /// </summary>
        public bool SuppressDebugOutput { get; set; } = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="FirmwareTraceListener"/> class.
        /// </summary>
        public FirmwareTraceListener()
            : base(SourceName)
        {
        }

        /// <summary>
        /// Initializes a new instance with a name.
        /// </summary>
        /// <param name="name">The listener name.</param>
        public FirmwareTraceListener(string name)
            : base(name)
        {
        }

        /// <summary>
        /// Writes a message to the trace listener.
        /// </summary>
        /// <param name="message">The message to write.</param>
        public override void Write(string message)
        {
            if (message == null) return;

            if (!SuppressDebugOutput)
            {
                Debug.Write(message);
            }

            OnTrace?.Invoke(message, TraceLevel.Verbose);
        }

        /// <summary>
        /// Writes a message followed by a line terminator to the trace listener.
        /// </summary>
        /// <param name="message">The message to write.</param>
        public override void WriteLine(string message)
        {
            if (message == null) return;

            if (!SuppressDebugOutput)
            {
                Debug.WriteLine(message);
            }

            OnTrace?.Invoke(message, TraceLevel.Info);
        }

        /// <summary>
        /// Writes the specified message with the given trace level.
        /// </summary>
        /// <param name="message">The message to write.</param>
        /// <param name="level">The trace event level.</param>
        public void WriteLine(string message, TraceLevel level)
        {
            if (message == null) return;

            string formatted = FormatMessage(message, level);

            if (!SuppressDebugOutput)
            {
                Debug.WriteLine(formatted);
            }

            OnTrace?.Invoke(formatted, level);
        }

        /// <summary>
        /// Formats a trace message with timestamp and level prefix.
        /// </summary>
        /// <param name="message">The raw message.</param>
        /// <param name="level">The trace level.</param>
        /// <returns>A formatted message string.</returns>
        internal static string FormatMessage(string message, TraceLevel level)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            return string.Format(CultureInfo.InvariantCulture, "[{0}] [{1}] [{2}] {3}", timestamp, level.ToString().ToUpperInvariant(), SourceName, message);
        }
    }
}
