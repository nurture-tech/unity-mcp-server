using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

// FIXME HACK
namespace System.Runtime.CompilerServices
{
    class IsExternalInit { }
}

namespace Nurture.MCP.Editor
{
    public static class UnityLoggerExtensions
    {
        public delegate Task<T> AsyncAction<T>();
        public delegate Task AsyncAction();

        public record WithLogResult<T>(T Result, string Logs);

        public record LogEntry(LogType logType, string message, string stackTrace);

        public static async Task<string> WithLogs(AsyncAction action, bool includeStackTrace = true)
        {
            List<LogEntry> buffer = new List<LogEntry>();

            Application.LogCallback handler = (message, stackTrace, type) =>
            {
                if (buffer.Any(l => l.message == message))
                {
                    // Ignore duplicate messages
                    return;
                }
                buffer.Add(
                    new LogEntry(
                        type,
                        message,
                        type != LogType.Log && includeStackTrace ? stackTrace : null
                    )
                );
            };

            Application.logMessageReceived += handler;
            await action();
            // NOTE: This could include log messages from interleaved async operations
            Application.logMessageReceived -= handler;
            return buffer
                .Select(l =>
                    $"[{Enum.GetName(typeof(LogType), l.logType)}] {l.message} {l.stackTrace}"
                )
                .Aggregate((a, b) => $"{a}\n{b}");
        }

        public static async Task<WithLogResult<T>> WithLogs<T>(
            AsyncAction<T> action,
            bool includeStackTrace = true
        )
        {
            List<LogEntry> buffer = new List<LogEntry>();

            Application.LogCallback handler = (message, stackTrace, type) =>
            {
                // TODO: Translate log type to string
                buffer.Add(
                    new LogEntry(
                        type,
                        message,
                        type != LogType.Log && includeStackTrace ? stackTrace : null
                    )
                );
            };

            Application.logMessageReceived += handler;
            var result = await action();
            Application.logMessageReceived -= handler;

            // NOTE: This could include log messages from interleaved async operations
            string logs = "";

            if (buffer.Count > 0)
            {
                logs = buffer
                    .Select(l =>
                        $"[{Enum.GetName(typeof(LogType), l.logType)}] {l.message} {l.stackTrace}"
                    )
                    .Aggregate((a, b) => $"{a}\n{b}");
            }

            return new WithLogResult<T>(result, logs);
        }
    }
}
