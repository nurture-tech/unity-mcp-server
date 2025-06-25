using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Object = UnityEngine.Object;

namespace Nurture.MCP.Editor
{
    class UnityMcpLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly LogLevel[] _levels;

        public UnityMcpLogger(string categoryName = null, LogLevel[] levels = null)
        {
            _categoryName = categoryName;
            _levels = levels;
        }

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _levels == null || _levels.Contains(logLevel);
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter
        )
        {
            Debug.Log($"[MCP] {logLevel}: {formatter(state, exception)}");
        }
    }

    class UnityLoggerFactory : ILoggerFactory
    {
        private LogLevel[] _levels;

        public UnityLoggerFactory(LogLevel[] levels)
        {
            _levels = levels;
        }

        public void Dispose() { }

        public ILogger CreateLogger(string categoryName)
        {
            return new UnityMcpLogger(categoryName, _levels);
        }

        public void AddProvider(ILoggerProvider provider)
        {
            throw new System.NotImplementedException();
        }
    }

    class UnityMcpLogHandler : ILogHandler
    {
        private static ILogHandler _defaultLogger;

        public UnityMcpLogHandler()
        {
            _defaultLogger = Debug.unityLogger.logHandler;
        }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            if (!format.StartsWith("{"))
            {
                return;
            }

            _defaultLogger.LogFormat(logType, context, format, args);
            ;
        }

        public void LogException(Exception exception, Object context)
        {
            return;
        }
    }
}
