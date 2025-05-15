#if !NO_MCP
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;
using Nurture.MCP.Editor;

namespace Nurture.MCP.Editor.Services
{
    [McpServerToolType]
    public static class ScriptService
    {
        public struct ScriptInfo
        {
            public string Guid { get; set; }
            public string Path { get; set; }
            public string Code { get; set; }
        }

        public struct MCPParameterInfo
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public bool IsOptional { get; set; }
            public object DefaultValue { get; set; }
        }

        public struct MCPMethodInfo
        {
            public string Name { get; set; }
            public string ReturnType { get; set; }
            public List<MCPParameterInfo> Parameters { get; set; }
        }

        public struct MCPFieldInfo
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public bool ReadOnly { get; set; }
        }

        public class TypeInfo
        {
            public List<MCPMethodInfo> Methods { get; set; }
            public List<MCPFieldInfo> Fields { get; set; }
        }

        [McpServerTool(
            Destructive = true,
            Idempotent = true,
            OpenWorld = true,
            ReadOnly = false,
            Title = "Create Unity Script",
            Name = "create_script"
        )]
        // FIXME: We instruct the agent to stop after running this tool to allow for the Domain Reload which will restart the MCP server.
        [Description(
            @"Create or replace a C# code file at the given path. 
            This also checks to make sure the script compiles. 
            Use this tool instead of generic file creation tools when working with Unity C# code.
            After running this tool, don't run additional tools. Ask the user to tell you when the script is done compiling."
        )]
        internal static Task<ScriptInfo> CreateScript(
            SynchronizationContext context,
            string code,
            [Description("The names of .NET assemblies to reference when compiling the script.")]
                List<string> assemblies,
            string path,
            CancellationToken cancellationToken,
            IProgress<ProgressNotificationValue> progress,
            [Description("Whether the script is an editor script or a play mode script.")]
                bool editor = false
        )
        {
            return context.Run(
                async () =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    progress.Report(
                        new ProgressNotificationValue()
                        {
                            Progress = 0f,
                            Message = "Compiling script...",
                            Total = 1.0f,
                        }
                    );
                    CompileCode(code, assemblies, editor);
                    await File.WriteAllTextAsync(path, code);
                    progress.Report(
                        new ProgressNotificationValue()
                        {
                            Progress = 0.5f,
                            Message = "Refreshing asset database...",
                            Total = 1.0f,
                        }
                    );
                    AssetDatabase.Refresh();
                    return new ScriptInfo()
                    {
                        Guid = AssetDatabase.AssetPathToGUID(path),
                        Path = path,
                        Code = code,
                    };
                },
                cancellationToken
            );
        }

        [McpServerTool(
            Destructive = true,
            Idempotent = false,
            OpenWorld = true,
            ReadOnly = false,
            Title = "Unity Execute Code",
            Name = "execute_code"
        )]
        [Description(
            "Execute code inside the Unity editor. Do not include any `using` statements."
        )]
        public static Task<UnityLoggerExtensions.WithLogResult<string>> ExecuteCode(
            SynchronizationContext context,
            [Description(
                @"The C# class to compile and execute which follows the format:      

                    using ModelContextProtocol;
                    using System.Threading;
                    using System.Threading.Tasks;
                    {{ using }}

                    public class CodeExecutor
                    {
                        public static async Task<string> Execute(CancellationToken cancellationToken, IProgress<ProgressNotificationValue> progress)
                        {
                            {{ body }}
                            return {{ result }};
                        }
                    }
                "
            )]
                string code,
            [Description("The names of .NET assemblies to reference when compiling the class.")]
                List<string> assemblies,
            CancellationToken cancellationToken,
            IProgress<ProgressNotificationValue> progress
        )
        {
            return context.Run(
                async () =>
                {
                    await EditorExtensions.EnsureNotPlaying(progress, cancellationToken, 0.1f);

                    try
                    {
                        EditorApplication.LockReloadAssemblies();
                        // Create a method that wraps the code
                        progress.Report(
                            new ProgressNotificationValue()
                            {
                                Progress = 0.5f,
                                Message = "Compiling script...",
                                Total = 1.0f,
                            }
                        );
                        var assembly = CompileCode(code, assemblies, true);
                        var type = assembly.GetType("CodeExecutor");
                        var method = type.GetMethod("Execute");
                        progress.Report(
                            new ProgressNotificationValue()
                            {
                                Progress = 0.75f,
                                Message = "Executing script...",
                                Total = 1.0f,
                            }
                        );

                        try
                        {
                            await EditorExtensions.FocusSceneView(cancellationToken);

                            var result = await UnityLoggerExtensions.WithLogs(
                                () =>
                                    (
                                        method.Invoke(
                                            null,
                                            new object[] { cancellationToken, progress }
                                        ) as Task<string>
                                    ),
                                // Don't log stack traces since they'll be useless (no backing file)
                                false
                            );

                            await EditorExtensions.FocusSceneView(cancellationToken);

                            return result;
                        }
                        catch (Exception e)
                        {
                            throw new McpException($"Script execution failed: {e.Message}", e);
                        }
                    }
                    finally
                    {
                        EditorApplication.UnlockReloadAssemblies();
                    }
                },
                cancellationToken
            );
        }

        private static Assembly CompileCode(
            string wrappedCode,
            List<string> assemblies,
            bool editor
        )
        {
            // Use Mono's built-in compiler
            var options = new CompilerParameters { GenerateInMemory = true };

            if (Settings.Instance.AlwaysIncludedAssemblies != null)
            {
                assemblies.AddRange(Settings.Instance.AlwaysIncludedAssemblies);
            }

            assemblies.AddRange(
                new List<string>
                {
                    "netstandard",
                    "System.Core",
                    "UnityEngine",
                    "UnityEngine.CoreModule",
                    "UnityEngine.UIModule",
                    "UnityEngine.PhysicsModule",
                    "UnityEngine.AnimationModule",
                    "UnityEngine.AudioModule",
                    "UnityEngine.DirectorModule",
                    "UnityEngine.ParticleSystemModule",
                    "Assembly-CSharp",
                }
            );

            if (editor)
            {
                assemblies.AddRange(
                    new List<string>
                    {
                        "UnityEditor",
                        "UnityEditor.CoreModule",
                        "ModelContextProtocol",
                    }
                );
            }

            foreach (var assemblyName in assemblies)
            {
                var assembly =
                    TypeExtensions.FindAssembly(assemblyName)
                    ?? throw new McpException($"Assembly {assemblyName} not found");
                if (!options.ReferencedAssemblies.Contains(assembly.Location))
                {
                    options.ReferencedAssemblies.Add(assembly.Location);
                }
            }

            // Compile and execute
            using var provider = new Microsoft.CSharp.CSharpCodeProvider();
            var results = provider.CompileAssemblyFromSource(options, wrappedCode);
            if (results.Errors.HasErrors)
            {
                var errors = string.Join(
                    "\n",
                    results
                        .Errors.Cast<CompilerError>()
                        .Where(e => !e.IsWarning)
                        .Select(e => $"Line {e.Line}: [{e.ErrorNumber}] {e.ErrorText}")
                );
                throw new McpException($"Compilation failed:\n{errors}");
            }

            return results.CompiledAssembly;
        }

        [McpServerTool(
            Destructive = false,
            Idempotent = true,
            OpenWorld = false,
            ReadOnly = true,
            Title = "Unity Get Type Info",
            Name = "get_type_info"
        )]
        [Description(
            "Get public fields and methods on a Unity fully qualified type name, including the assembly. This is primarily useful for correcting errors when calling the `execute_code` tool."
        )]
        public static async Task<TypeInfo> GetTypeInfo(
            SynchronizationContext context,
            string typeName,
            string assemblyName,
            CancellationToken cancellationToken
        )
        {
            return await context.Run(
                () =>
                {
                    var assembly =
                        TypeExtensions.FindAssembly(assemblyName)
                        ?? throw new McpException($"Assembly {assemblyName} not found");
                    var type =
                        assembly.GetType(typeName)
                        ?? throw new McpException($"Type {typeName} not found");

                    return new TypeInfo()
                    {
                        Methods = type.GetMethods()
                            .Select(m => new MCPMethodInfo()
                            {
                                Name = m.Name,
                                ReturnType = m.ReturnType.AssemblyQualifiedName,
                                Parameters = m.GetParameters()
                                    .Select(p => new MCPParameterInfo()
                                    {
                                        Name = p.Name,
                                        Type = p.ParameterType.AssemblyQualifiedName,
                                        IsOptional = p.IsOptional,
                                        DefaultValue = p.DefaultValue,
                                    })
                                    .ToList(),
                            })
                            .ToList(),
                        Fields = type.GetFields()
                            .Select(f => new MCPFieldInfo()
                            {
                                Name = f.Name,
                                Type = f.FieldType.AssemblyQualifiedName,
                                ReadOnly = f.IsInitOnly,
                            })
                            .ToList(),
                    };
                },
                cancellationToken
            );
        }
    }
}

#endif
