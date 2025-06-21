using System;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Nurture.MCP.Editor
{
    public class UnityMcpLogger : ILogger
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

    public class UnityMcpLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new UnityMcpLogger();

        public void Dispose() { }
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
}
