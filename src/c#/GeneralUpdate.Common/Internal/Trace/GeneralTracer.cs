using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace GeneralUpdate.Common.Internal;

public static class GeneralTracer
{
    private static readonly object _lockObj = new();
    private static bool _isTracingEnabled;
    private static string _currentLogDate;
    private static TextWriterTraceListener _fileListener;
  
    static GeneralTracer()
    {
        Trace.Listeners.Clear();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Trace.Listeners.Add(new WindowsOutputDebugListener());
        }

        Trace.Listeners.Add(new TextWriterTraceListener(Console.Out) { Name = "ConsoleListener" });
        
        InitializeFileListener();

        if (Debugger.IsAttached)
            Trace.Listeners.Add(new DefaultTraceListener());

        Trace.AutoFlush = true;
        _isTracingEnabled = true;
    }
    
    private static void InitializeFileListener()
    {
        //Ensure that log files are rotated on a daily basis
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        if (today == _currentLogDate && _fileListener != null)
            return;

        if (_fileListener != null)
        {
            Trace.Listeners.Remove(_fileListener);
            _fileListener.Flush();
            _fileListener.Close();
            _fileListener.Dispose();
        }

        var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        Directory.CreateDirectory(logDir);

        var logFileName = Path.Combine(logDir, $"generalupdate-trace {today}.log");
        _fileListener = new TextWriterTraceListener(logFileName) { Name = "FileListener" };
            
        Trace.Listeners.Add(_fileListener);
        _currentLogDate = today;
    }

    public static void Debug(string message) => WriteTraceMessage(TraceLevel.Verbose, message);

    public static void Info(string message) => WriteTraceMessage(TraceLevel.Info, message);

    public static void Warn(string message) => WriteTraceMessage(TraceLevel.Warning, message);

    public static void Error(string message) => WriteTraceMessage(TraceLevel.Error, message);

    public static void Fatal(string message) => WriteTraceMessage(TraceLevel.Off, message);

    public static void Error(string message, Exception ex)
    {
        var fullMessage = $"{message}{Environment.NewLine} Exception Details: {ex}";
        WriteTraceMessage(TraceLevel.Error, fullMessage);
    }

    public static void Fatal(string message, Exception ex)
    {
        var fullMessage = $"{message}{Environment.NewLine} Exception Details: {ex}";
        WriteTraceMessage(TraceLevel.Off, fullMessage);
    }

    public static void SetTracingEnabled(bool enabled)
    {
        lock (_lockObj)
        {
            Trace.AutoFlush = enabled;
            _isTracingEnabled = enabled;
            foreach (TraceListener listener in Trace.Listeners)
            {
                listener.Filter = enabled ? null : new EventTypeFilter(SourceLevels.Off);
            }
        }
    }

    public static bool IsTracingEnabled()
    {
        lock (_lockObj)
        {
            return _isTracingEnabled;
        }
    }

    private static void WriteTraceMessage(TraceLevel level, string message)
    {
        if(!IsTracingEnabled())
            return;
        
        InitializeFileListener();
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var levelName = GetLevelName(level);
        var fullMessage = string.Empty;

        try
        {
            var stackFrame = new StackFrame(2, true);
            var method = stackFrame.GetMethod();
            var className = method.DeclaringType?.Name ?? "UnknownType";
            var methodName = method.Name;
            var lineNumber = stackFrame.GetFileLineNumber();
            var lineInfo = lineNumber > 0 ? $"Line {lineNumber}" : "Line N/A (Line numbers may not be displayed in Release mode)";
            fullMessage = $"[{timestamp}] [{levelName}] {className}.{methodName} ({lineInfo}): {message}";
        }
        catch
        {
            fullMessage = $"[{timestamp}] [{levelName}] : {message}";
        }

        Trace.WriteLine(fullMessage);
    }

    private static string GetLevelName(TraceLevel level) => level switch
    {
        TraceLevel.Verbose => "DEBUG",
        TraceLevel.Info => "INFO",
        TraceLevel.Warning => "WARN",
        TraceLevel.Error => "ERROR",
        TraceLevel.Off => "FATAL",
        _ => "UNKNOWN"
    };

    public static void Dispose()
    {
        lock (_lockObj)
        {
            if (_fileListener is not null)
            {
                _fileListener.Flush();
                _fileListener.Close();
                _fileListener.Dispose();
                _fileListener = null;
            }

            Trace.Listeners.Clear();
        }
    }
}