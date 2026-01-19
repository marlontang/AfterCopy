using System;
using System.IO;

namespace AfterCopy.Helpers
{
    public static class Logger
    {
        private static string LogPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");

        public static void Log(string message)
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                File.AppendAllText(LogPath, logEntry);
            }
            catch
            {
                // Best effort logging, suppress errors if FS is locked/readonly
            }
        }

        public static void LogError(string context, Exception ex)
        {
            string errorDetails = $@"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERROR] {context}
Message: {ex.Message}
StackTrace:
{ex.StackTrace}
--------------------------------------------------
";
            try
            {
                File.AppendAllText(LogPath, errorDetails);
            }
            catch { }
        }
    }
}
