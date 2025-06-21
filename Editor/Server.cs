#if !NO_MCP

using UnityEditor;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using UnityEngine;
using System.Reflection;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace Nurture.MCP.Editor
{
    [InitializeOnLoad]
    public class Server
    {
        private static StdioServerTransport _transport;

        static Server()
        {
            // Register for domain reload to stop the server
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            Start();
        }

        private static void OnBeforeAssemblyReload()
        {
            Stop();
        }

        private static void Start()
        {
            Debug.Log("[MCP] Starting server");

            McpServerOptions options = new()
            {
                ServerInfo = new() { Name = "Nurture Unity MCP", Version = "1.0.0" },
                Capabilities = new(),
                ServerInstructions =
                    @"When copying a file inside of `Assets` folder, use the `Unity_CopyAsset` tool instead of generic file tools. 
                    Do not use generic codebase search or file search tools on any files in the `Assets` folder other than for *.cs files.",
            };

            var services = new ServiceCollection()
                .AddSingleton(SynchronizationContext.Current)
                .BuildServiceProvider();

            var toolOptions = new McpServerToolCreateOptions() { Services = services };

            CollectTools(options, toolOptions);
            CollectPrompts(options);

            _transport = new StdioServerTransport(options, new UnityLoggerFactory());
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

        private static async Task Stop()
        {
            await _transport.DisposeAsync();
            _transport = null;
        }
    }
}

#endif
