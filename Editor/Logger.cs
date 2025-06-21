using System;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Object = UnityEngine.Object;

namespace Nurture.MCP.Editor
{
    class UnityMcpLogger : ILogger
    {
        private readonly string _categoryName;

        public UnityMcpLogger(string categoryName = null)
        {
            _categoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
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
        public void Dispose() { }

        public ILogger CreateLogger(string categoryName)
        {
            return new UnityMcpLogger(categoryName);
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
            
            _defaultLogger.LogFormat(logType, context, format, args);;
        }

        public void LogException(Exception exception, Object context)
        {
            return;
        }
    }
}
