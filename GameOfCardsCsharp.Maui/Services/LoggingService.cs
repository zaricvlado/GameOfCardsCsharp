using Microsoft.Extensions.Logging;

namespace GameOfCardsCsharp.Maui.Services
{
    public class LoggingService
    {
        private bool _isEnabled = false;  // Default: OFF
        private LogLevel _currentLevel = LogLevel.Debug;
        private string? _currentLogFilePath;
        private FileLoggerProvider? _currentProvider;

        public bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        public LogLevel CurrentLevel
        {
            get => _currentLevel;
            set => _currentLevel = value;
        }

        public string? CurrentLogFilePath => _currentLogFilePath;

        public void SetLevel(string level)
        {
            CurrentLevel = level switch
            {
                "Debug" => LogLevel.Debug,
                "Info" => LogLevel.Information,
                "Warning" => LogLevel.Warning,
                "Error" => LogLevel.Error,
                _ => LogLevel.Information
            };
        }
        public FileLoggerProvider? CreateNewLogFile()
        {
            if (!_isEnabled)
                return null;

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _currentLogFilePath = Path.Combine(
                FileSystem.AppDataDirectory,
                "logs",
                $"tablic_game_{timestamp}.log");

            _currentProvider = new FileLoggerProvider(_currentLogFilePath, _currentLevel);
            return _currentProvider;
        }

        public void SetCurrentProvider(FileLoggerProvider? provider)
        {
            _currentProvider = provider;
        }
    }
}