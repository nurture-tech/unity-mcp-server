#if !NO_MCP

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Server;
using UnityEngine;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Utils.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Nurture.MCP.Editor
{
    public class HttpServer : IDisposable
    {
        private class DebugLogger : Microsoft.Extensions.Logging.ILogger
        {
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

        private class DebugLoggerProvider : ILoggerProvider
        {
            public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) =>
                new DebugLogger();

            public void Dispose() { }
        }

        private readonly HttpListener _listener;
        private readonly McpServerOptions _options;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private readonly ServiceProvider _services;

        private readonly ConcurrentDictionary<string, SseResponseStreamTransport> _sessions = new(
            StringComparer.Ordinal
        );

        public HttpServer(string prefix, McpServerOptions options, ServiceProvider services)
        {
            _options = options;
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _cancellationTokenSource = new CancellationTokenSource();
            _services = services;
        }

        public void Start()
        {
            Debug.Log("[MCP] Starting http server");
            _listener.Start();
            _ = RunServerAsync(_cancellationTokenSource.Token);
        }

        private async Task RunServerAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Debug.Log("[MCP] Waiting for next http request");
                    var context = await _listener.GetContextAsync();
                    _ = HandleRequestAsync(context, cancellationToken);
                }
                catch (Exception) when (cancellationToken.IsCancellationRequested)
                {
                    // Normal shutdown
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        private async Task HandleRequestAsync(
            HttpListenerContext context,
            CancellationToken cancellationToken
        )
        {
            Debug.Log($"[MCP] Handling http request: {context.Request.Url.PathAndQuery}");

            try
            {
                if (context.Request.Url.PathAndQuery == "/sse")
                {
                    await HandleSSEConnectionAsync(context, cancellationToken);
                }
                else if (
                    context.Request.Url.AbsolutePath == "/message"
                    && context.Request.HttpMethod == "POST"
                )
                {
                    await HandleMessageAsync(context);
                }
                else
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
        }

        private async Task HandleSSEConnectionAsync(
            HttpListenerContext context,
            CancellationToken cancellationToken
        )
        {
            Debug.Log("[MCP] Handling SSE connection");

            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.Add("Cache-Control", "no-cache");
            context.Response.Headers.Add("Connection", "keep-alive");
            context.Response.KeepAlive = true;

            var stream = context.Response.OutputStream;

            var sessionId = Guid.NewGuid().ToString();

            // Create SSE transport
            var sseTransport = new SseResponseStreamTransport(
                stream,
                $"/message?sessionId={sessionId}"
            );
            _ = sseTransport.RunAsync(cancellationToken);

            _sessions.TryAdd(sessionId, sseTransport);

            Debug.Log($"[MCP] Created session: {sessionId}");

            using ILoggerFactory factory = LoggerFactory.Create(builder =>
                builder.AddProvider(new DebugLoggerProvider())
            );

            // Create and run MCP server
            try
            {
                await using var server = McpServerFactory.Create(
                    sseTransport,
                    _options,
                    factory,
                    _services
                );
                Debug.Log("[MCP] Mcp Server created for session");
                await server.RunAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Debug.Log("[MCP] Operation canceled");
                // Normal shutdown
            }
            finally
            {
                Debug.Log($"[MCP] Removing session: {sessionId}");
                _sessions.TryRemove(sessionId, out _);
            }
        }

        private async Task HandleMessageAsync(HttpListenerContext context)
        {
            Debug.Log($"[MCP] Handling http message: {context.Request.Url.PathAndQuery}");

            var sessionId = context.Request.QueryString["sessionId"];

            if (string.IsNullOrEmpty(sessionId))
            {
                Debug.LogError("[MCP] No session ID provided");
                context.Response.StatusCode = 400;
                context.Response.Close();
                return;
            }

            if (!_sessions.TryGetValue(sessionId, out var sseTransport))
            {
                Debug.LogError($"[MCP] No session found for ID: {sessionId}");
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            JsonRpcMessage message = await JsonSerializer.DeserializeAsync<JsonRpcMessage>(
                context.Request.InputStream,
                McpJsonUtilities.DefaultOptions
            );

            if (message == null)
            {
                Debug.LogError("[MCP] Invalid message received");
                context.Response.StatusCode = 400;
                context.Response.Close();
                return;
            }

            await sseTransport.OnMessageReceivedAsync(message, _cancellationTokenSource.Token);

            // Process the message and send response
            context.Response.StatusCode = 202;
            await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("Accepted"));
            context.Response.Close();
        }

        public void Stop()
        {
            Debug.Log("[MCP] Stopping http server...");
            _cancellationTokenSource.Cancel();
            _listener.Stop();
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource.Dispose();
            _listener.Close();
        }
    }
}

#endif
