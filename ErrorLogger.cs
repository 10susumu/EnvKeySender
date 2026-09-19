using System;
using System.IO;

namespace EnvKeySender
{
    public static class ErrorLogger
    {
        private static readonly string AppDirectory = AppContext.BaseDirectory;
        private static readonly string LogDirectory = Path.Combine(AppDirectory, "logs");
        public static readonly string LogPath = Path.Combine(LogDirectory, "error.log");

        static ErrorLogger()
        {
            Directory.CreateDirectory(LogDirectory);
        }

        public static void Write(string message)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Avoid crashing if logging fails.
            }
        }

        public static void WriteException(Exception ex, string context)
        {
            Write($"{context}{Environment.NewLine}Type: {ex.GetType().FullName}{Environment.NewLine}Message: {ex.Message}{Environment.NewLine}Stack: {ex.StackTrace}");
        }
    }
}
