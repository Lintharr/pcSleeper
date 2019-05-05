using log4net;
using System;

namespace PCSleeper
{
    internal static class Logger
    {
        private static readonly ILog _logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static void LogInfo(string message)
        {
            if (ConfigManager.EnableLogging)
                _logger.Info(message);
        }

        public static void LogWarning(string message, Exception exception = null)
        {
            if (ConfigManager.EnableLogging)
                _logger.Warn(message, exception);
        }

        public static void LogError(string message, Exception exception = null)
        {
            if (ConfigManager.EnableLogging)
                _logger.Error(message, exception);
        }

        public static void LogFatal(string message, Exception exception)
        {
            if (ConfigManager.EnableLogging)
                _logger.Fatal(message, exception);
        }
    }
}