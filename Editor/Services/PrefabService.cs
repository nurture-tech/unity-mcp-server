#if USE_MCP
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;

namespace Nurture.MCP.Editor.Services
{
    [McpServerToolType]
    public static class PrefabService
    {
        [McpServerTool(
            Destructive = false,
            Idempotent = true,
            OpenWorld = false,
            ReadOnly = true,
            Title = "Open Unity Prefab",
            Name = "open_prefab"
        )]
        [Description("Open a Unity prefab so that it can be edited.")]
        internal static async Task<SceneService.SceneIndexEntry> OpenPrefab(
            SynchronizationContext context,
            IProgress<ProgressNotificationValue> progress,
            string guid,
            CancellationToken cancellationToken
        )
        {
            return await context.Run(async () =>
            {
                await EditorExtensions.FocusSceneView(cancellationToken);

                // Ensure we're not playing
                await EditorExtensions.EnsureNotPlaying(progress, cancellationToken, 0.1f);

                // Go into prefab edit mode

                var guidPath = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(guidPath);
                bool result = AssetDatabase.OpenAsset(prefab);

                if (!result)
                {
                    throw new McpException("Failed to open prefab");
                }

                return new SceneService.SceneIndexEntry()
                {
                    Name = prefab.name,
                    Path = AssetDatabase.GUIDToAssetPath(guid),
                    Guid = guid,
                    BuildIndex = 0,
                    RootGameObjects = prefab
                        .GetComponentsInChildren<Transform>()
                        .Select(t => t.name)
                        .ToList(),
                };
            });
        }
    }
}

#endif
