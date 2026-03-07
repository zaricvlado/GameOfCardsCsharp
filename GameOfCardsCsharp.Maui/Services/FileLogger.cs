using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GameOfCardsCsharp.Maui.Services
{
    public class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly string _filePath;
        private readonly LogLevel _minimumLevel;
        private readonly object _lock = new object();

        public FileLogger(string categoryName, string filePath, LogLevel minimumLevel)
        {
            _categoryName = categoryName;
            _filePath = filePath;
            _minimumLevel = minimumLevel;
            
            // Ensure log directory exists
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLevel;

        public void Log<TState>(
            LogLevel logLevel, 
            EventId eventId, 
            TState state, 
            Exception? exception, 
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var level = logLevel.ToString().ToUpper().PadRight(11);
            var category = GetShortCategoryName(_categoryName);
            var message = formatter(state, exception);
            
            var logLine = $"{timestamp} [{level}] {category}: {message}";
            
            if (exception != null)
            {
                logLine += Environment.NewLine + exception.ToString();
            }

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_filePath, logLine + Environment.NewLine);
                }
                catch
                {
                    // Silently fail if we can't write to log file
                }
            }
        }

        private string GetShortCategoryName(string categoryName)
        {
            // Shorten category name: "GameOfCardsCsharp.Maui.TablicGamePage" → "TablicGamePage"
            var lastDot = categoryName.LastIndexOf('.');
            return lastDot >= 0 ? categoryName.Substring(lastDot + 1) : categoryName;
        }
    }

    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _filePath;
        private readonly LogLevel _minimumLevel;

        public FileLoggerProvider(string filePath, LogLevel minimumLevel = LogLevel.Debug)
        {
            _filePath = filePath;
            _minimumLevel = minimumLevel;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(categoryName, _filePath, _minimumLevel);
        }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }
}
