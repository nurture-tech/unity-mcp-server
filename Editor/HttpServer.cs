#if !NO_MCP

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;
using UnityEngine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Pipelines;
using System.Linq;

namespace Nurture.MCP.Editor
{
    public class HttpServer : IDisposable
    {
        private static readonly JsonTypeInfo<JsonRpcError> s_errorTypeInfo = GetRequiredJsonTypeInfo<JsonRpcError>();

        private static readonly string s_applicationJsonMediaType = "application/json";
        private static readonly string s_textEventStreamMediaType = "text/event-stream";

        
        private static JsonTypeInfo<T> GetRequiredJsonTypeInfo<T>() => (JsonTypeInfo<T>)McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(T));

        private sealed class HttpDuplexPipe : IDuplexPipe
        {
            public PipeReader Input => PipeReader.Create(context.Request.InputStream);
            public PipeWriter Output => PipeWriter.Create(context.Response.OutputStream);

            private readonly HttpListenerContext context;

            public HttpDuplexPipe(HttpListenerContext context)
            {
                this.context = context;
            }
        }

        private class HttpMcpSession<TTransport>
        where TTransport : ITransport
        {
            public string Id { get; private set; }
            public TTransport Transport { get; private set; }

             public IMcpServer? Server { get; set; }
            public Task? ServerRunTask { get; set; }

            private CancellationTokenSource _disposeCts = new();

            public CancellationToken SessionClosed => _disposeCts.Token;

            public HttpMcpSession(string sessionId, TTransport transport)
            {
                Id = sessionId;
                Transport = transport;
            }

            public async ValueTask DisposeAsync()
            {
                try
                {
                    _disposeCts.Cancel();

                    if (ServerRunTask is not null)
                    {
                        await ServerRunTask;
                    }
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    try
                    {
                        if (Server is not null)
                        {
                            await Server.DisposeAsync();
                        }
                    }
                    finally
                    {
                        await Transport.DisposeAsync();
                        _disposeCts.Dispose();
                    }
                }
            }
        }


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
        private readonly ILoggerFactory _loggerFactory;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ServiceProvider _services;

        private readonly ConcurrentDictionary<string, HttpMcpSession<StreamableHttpServerTransport>> _sessions =
            new(StringComparer.Ordinal);

        public HttpServer(string prefix, McpServerOptions options, ServiceProvider services)
        {
            _options = options;
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _cancellationTokenSource = new CancellationTokenSource();
            _services = services;
            _loggerFactory = LoggerFactory.Create(builder =>
                builder.AddProvider(new DebugLoggerProvider())
            );
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
            Debug.Log($"[MCP] Handling request: {context.Request.HttpMethod}");

            if (context.Request.HttpMethod == "POST")
            {
                await HandlePostRequestAsync(context);
            }
            else if (context.Request.HttpMethod == "GET")
            {
                await HandleGetRequestAsync(context);
            }
            else if (context.Request.HttpMethod == "DELETE")
            {
                await HandleDeleteRequestAsync(context);
            }
            else
            {
                throw new NotSupportedException($"HTTP method {context.Request.HttpMethod} is not supported");
            }
        }

        public async Task HandlePostRequestAsync(HttpListenerContext context)
        {
            // The Streamable HTTP spec mandates the client MUST accept both application/json and text/event-stream.
            // ASP.NET Core Minimal APIs mostly try to stay out of the business of response content negotiation,
            // so we have to do this manually. The spec doesn't mandate that servers MUST reject these requests,
            // but it's probably good to at least start out trying to be strict.
            string acceptHeaderRaw = context.Request.Headers["Accept"];
            // Parse accept headers
            string[] acceptHeaders = acceptHeaderRaw.Split(',');
            if (!acceptHeaders.Any(s => s.Trim() == s_applicationJsonMediaType || s.Trim() == s_textEventStreamMediaType))
            {
                await WriteJsonRpcErrorAsync(context,
                    "Not Acceptable: Client must accept both application/json and text/event-stream",
                    406);
                return;
            }

            var session = await GetOrCreateSessionAsync(context);
            if (session is null)
            {
                return;
            }

            InitializeSseResponse(context);
            var wroteResponse = await session.Transport.HandlePostRequest(new HttpDuplexPipe(context), session.SessionClosed);
            if (!wroteResponse)
            {
                // We wound up writing nothing, so there should be no Content-Type response header.
                context.Response.Headers["Content-Type"] = (string?)null;
                context.Response.StatusCode = 202;
            }
        }

        public async Task HandleGetRequestAsync(HttpListenerContext context)
        {
            string sessionId = context.Request.Headers["mcp-session-id"];
            var session = await GetSessionAsync(context, sessionId);
            if (session is null)
            {
                return;
            }

            InitializeSseResponse(context);

            // We should flush headers to indicate a 200 success quickly, because the initialization response
            // will be sent in response to a different POST request. It might be a while before we send a message
            // over this response body.
            await context.Response.OutputStream.FlushAsync(session.SessionClosed);
            await session.Transport.HandleGetRequest(context.Response.OutputStream, session.SessionClosed);
        }

        public async Task HandleDeleteRequestAsync(HttpListenerContext context)
        {
            var sessionId = context.Request.Headers["mcp-session-id"];
            if (_sessions.TryRemove(sessionId, out var session))
            {
                await session.DisposeAsync();
            }
        }

        private async ValueTask<HttpMcpSession<StreamableHttpServerTransport>?> GetSessionAsync(HttpListenerContext context, string sessionId)
        {
            HttpMcpSession<StreamableHttpServerTransport>? session;

            if (sessionId == null || !_sessions.TryGetValue(sessionId, out session))
            {
                // -32001 isn't part of the MCP standard, but this is what the typescript-sdk currently does.
                // One of the few other usages I found was from some Ethereum JSON-RPC documentation and this
                // JSON-RPC library from Microsoft called StreamJsonRpc where it's called JsonRpcErrorCode.NoMarshaledObjectFound
                // https://learn.microsoft.com/dotnet/api/streamjsonrpc.protocol.jsonrpcerrorcode?view=streamjsonrpc-2.9#fields
                await WriteJsonRpcErrorAsync(context, "Session not found", 404, -32001);
                return null;
            }

            context.Response.Headers["mcp-session-id"] = session.Id;
            return session;
        }

        private async ValueTask<HttpMcpSession<StreamableHttpServerTransport>?> GetOrCreateSessionAsync(HttpListenerContext context)
        {
            string sessionId = context.Request.Headers["mcp-session-id"];

            if (string.IsNullOrEmpty(sessionId))
            {
                return await StartNewSessionAsync(context);
            }
            else
            {
                return await GetSessionAsync(context, sessionId);
            }
        }

        private async ValueTask<HttpMcpSession<StreamableHttpServerTransport>> StartNewSessionAsync(HttpListenerContext context)
        {
            string sessionId;
            StreamableHttpServerTransport transport;

            sessionId = MakeNewSessionId();
            transport = new();
            context.Response.Headers["mcp-session-id"] = sessionId;

            var session = await CreateSessionAsync(context, transport, sessionId);

            if (!_sessions.TryAdd(sessionId, session))
            {
                throw new Exception($"Unreachable given good entropy! Session with ID '{sessionId}' has already been created.");
            }

            return session;
        }

        private async ValueTask<HttpMcpSession<StreamableHttpServerTransport>> CreateSessionAsync(
            HttpListenerContext context,
            StreamableHttpServerTransport transport,
            string sessionId)
        {
            var mcpServerServices = _services;
            var mcpServerOptions = _options;

            var server = McpServerFactory.Create(transport, mcpServerOptions, _loggerFactory, mcpServerServices);

            var session = new HttpMcpSession<StreamableHttpServerTransport>(sessionId, transport)
            {
                Server = server,
            };

            session.ServerRunTask = server.RunAsync(session.SessionClosed);

            return session;
        }

        private static ValueTask WriteJsonRpcErrorAsync(HttpListenerContext context, string errorMessage, int statusCode, int errorCode = -32000)
        {
            var jsonRpcError = new JsonRpcError
            {
                Error = new()
                {
                    Code = errorCode,
                    Message = errorMessage,
                },
            };

            var json = JsonSerializer.Serialize(jsonRpcError, s_errorTypeInfo);
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = s_applicationJsonMediaType;
            context.Response.ContentLength64 = json.Length;
            return context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(json));
        }

        internal static void InitializeSseResponse(HttpListenerContext context)
        {
            context.Response.Headers["Content-Type"] = "text/event-stream";
            context.Response.Headers["Cache-Control"] = "no-cache,no-store";

            // Make sure we disable all response buffering for SSE.
            context.Response.Headers["Content-Encoding"] = "identity";
            context.Response.SendChunked = true;
        }

        internal static string MakeNewSessionId()
        {
            return Guid.NewGuid().ToString();
        }

        public async Task Stop()
        {
            Debug.Log("[MCP] Stopping http server...");
            _cancellationTokenSource.Cancel();
            foreach (var session in _sessions.Values)
            {
                await session.DisposeAsync();
            }
            _listener.Stop();
        }

        public async void Dispose()
        {
            await Stop();
            _cancellationTokenSource.Dispose();
            _listener.Close();
        }
    }
}

#endif
