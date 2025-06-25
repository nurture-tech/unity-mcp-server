#if !NO_MCP

using System;
using UnityEditor;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using UnityEngine;
using System.Reflection;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Nurture.MCP.Editor
{
    [InitializeOnLoad]
    public class Server
    {
        private static CancellationTokenSource _cancellationTokenSource;
        private static McpServerOptions _options;
        private static IServiceProvider _services;

        static Server()
        {
            // Register for domain reload to stop the server
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            // Get whether "-mcp" command line parameter was passed to Unity
            bool mcp = System
                .Environment.GetCommandLineArgs()
                .Any(arg => string.Equals(arg, "-mcp", System.StringComparison.OrdinalIgnoreCase));

            if (!mcp)
            {
                return;
            }

            Start();
        }

        private static void OnBeforeAssemblyReload()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            Stop();
        }

        private static void Start()
        {
            Debug.Log("[MCP] Starting server");

            Debug.unityLogger.logHandler = new UnityMcpLogHandler();

            _options = new()
            {
                ServerInfo = new() { Name = "Nurture Unity MCP", Version = "1.0.0" },
                Capabilities = new(),
                ServerInstructions =
                    @"When copying a file inside of `Assets` folder, use the `Unity_CopyAsset` tool instead of generic file tools. 
                    Do not use generic codebase search or file search tools on any files in the `Assets` folder other than for *.cs files.",
            };

            _services = new ServiceCollection()
                .AddSingleton(SynchronizationContext.Current)
                .BuildServiceProvider();

            var toolOptions = new McpServerToolCreateOptions() { Services = _services };

            CollectTools(_options, toolOptions);
            CollectPrompts(_options);

            _cancellationTokenSource = new();

            Task.Run(RunServer);
        }

        private static async Task RunServer()
        {
            using var loggerFactory = new UnityLoggerFactory(
                new LogLevel[] { LogLevel.Error, LogLevel.Critical, LogLevel.Warning }
            );
            await using var stdioTransport = new StdioServerTransport(_options, loggerFactory);
            await using IMcpServer server = McpServerFactory.Create(
                stdioTransport,
                _options,
                loggerFactory,
                _services
            );
            await server.RunAsync(_cancellationTokenSource.Token);
        }

        private static void CollectTools(
            McpServerOptions options,
            McpServerToolCreateOptions toolOptions
        )
        {
            var toolAssembly = Assembly.GetCallingAssembly();
            var toolTypes =
                from t in toolAssembly.GetTypes()
                where t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null
                select t;

            ToolsCapability tools = new() { ToolCollection = new() };

            foreach (var toolType in toolTypes)
            {
                foreach (
                    var toolMethod in toolType.GetMethods(
                        BindingFlags.Public
                            | BindingFlags.NonPublic
                            | BindingFlags.Static
                            | BindingFlags.Instance
                    )
                )
                {
                    if (toolMethod.GetCustomAttribute<McpServerToolAttribute>() is not null)
                    {
                        var tool = McpServerTool.Create(toolMethod, options: toolOptions);
                        tools.ToolCollection.Add(tool);
                    }
                }
            }

            if (tools.ToolCollection.Count > 0)
            {
                options.Capabilities.Tools = tools;
            }
        }

        private static void CollectPrompts(McpServerOptions options)
        {
            var promptAssembly = Assembly.GetCallingAssembly();
            var promptTypes =
                from t in promptAssembly.GetTypes()
                where t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null
                select t;

            PromptsCapability prompts = new() { PromptCollection = new() };

            foreach (var promptType in promptTypes)
            {
                foreach (
                    var promptMethod in promptType.GetMethods(
                        BindingFlags.Public
                            | BindingFlags.NonPublic
                            | BindingFlags.Static
                            | BindingFlags.Instance
                    )
                )
                {
                    if (promptMethod.GetCustomAttribute<McpServerPromptAttribute>() is not null)
                    {
                        var prompt = promptMethod.IsStatic
                            ? McpServerPrompt.Create(promptMethod)
                            : McpServerPrompt.Create(promptMethod, promptType);

                        prompts.PromptCollection.Add(prompt);
                    }
                }
            }

            if (prompts.PromptCollection.Count > 0)
            {
                options.Capabilities.Prompts = prompts;
            }
        }

        private static void Stop()
        {
            Debug.Log("[MCP] Stopping server");
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = null;
        }
    }
}

#endif
