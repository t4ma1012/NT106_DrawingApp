// ============================================================
// SharedLib/Logging/Logger.cs
// Tuần 9 — Centralized Logging System
// Ghi tất cả Console output + exceptions vào file log
// ============================================================
using System;
using System.IO;
using System.Text;

namespace SharedLib.Logging
{
    /// <summary>
    /// Centralized logger: ghi vào file + console cùng lúc.
    /// File log: logs/yyyy-MM-dd_HHmmss.log
    /// Format: [yyyy-MM-dd HH:mm:ss.fff] [Level] [Component] Message
    /// </summary>
    public static class Logger
    {
        private static readonly string LogDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "logs");
        private static readonly object _lock = new object();
        private static StreamWriter _logFile;
        private static bool _initialized = false;

        public enum LogLevel { DEBUG, INFO, WARNING, ERROR }

        /// <summary>Khởi tạo logger. Gọi MỘT lần khi ứng dụng start.</summary>
        public static void Initialize(string logFileName = null)
        {
            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    if (!Directory.Exists(LogDir))
                        Directory.CreateDirectory(LogDir);

                    string filename = logFileName ?? $"{DateTime.Now:yyyy-MM-dd_HHmmss}.log";
                    string logPath = Path.Combine(LogDir, filename);

                    _logFile = new StreamWriter(logPath, true, Encoding.UTF8)
                    { AutoFlush = true };

                    _initialized = true;
                    Info("Logger", "Logger initialized: " + logPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Logger] Khởi tạo lỗi: {ex.Message}");
                }
            }
        }

        /// <summary>Log thông báo INFO.</summary>
        public static void Info(string component, string message)
            => Log(LogLevel.INFO, component, message);

        /// <summary>Log cảnh báo WARNING.</summary>
        public static void Warning(string component, string message)
            => Log(LogLevel.WARNING, component, message);

        /// <summary>Log lỗi ERROR.</summary>
        public static void Error(string component, string message)
            => Log(LogLevel.ERROR, component, message);

        /// <summary>Log exception chi tiết.</summary>
        public static void Exception(string component, Exception ex)
            => Log(LogLevel.ERROR, component, 
               $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");

        /// <summary>Log debug (chỉ khi DEBUG mode).</summary>
        public static void Debug(string component, string message)
        {
#if DEBUG
            Log(LogLevel.DEBUG, component, message);
#endif
        }

        private static void Log(LogLevel level, string component, string message)
        {
            lock (_lock)
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string formattedMessage = $"[{timestamp}] [{level,-7}] [{component,-20}] {message}";

                // Ghi vào console
                var oldColor = Console.ForegroundColor;
                Console.ForegroundColor = level switch
                {
                    LogLevel.ERROR => ConsoleColor.Red,
                    LogLevel.WARNING => ConsoleColor.Yellow,
                    LogLevel.DEBUG => ConsoleColor.Gray,
                    _ => ConsoleColor.White
                };
                Console.WriteLine(formattedMessage);
                Console.ForegroundColor = oldColor;

                // Ghi vào file nếu initialized
                if (_initialized && _logFile != null)
                {
                    try
                    {
                        _logFile.WriteLine(formattedMessage);
                    }
                    catch { /* Bỏ qua lỗi file */ }
                }
            }
        }

        /// <summary>Đóng log file khi ứng dụng tắt.</summary>
        public static void Close()
        {
            lock (_lock)
            {
                _logFile?.Flush();
                _logFile?.Close();
                _logFile?.Dispose();
            }
        }
    }
}
