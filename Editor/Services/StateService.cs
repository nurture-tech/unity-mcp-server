#if !NO_MCP

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nurture.MCP.Editor;
using Nurture.MCP.Editor.Services;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace Nurture.Mcp.Editor.Services
{
    [McpServerToolType]
    public static class StateService
    {
        public struct UnityState
        {
            public List<SceneService.SceneIndexEntry> OpenScenes { get; set; }
            public string UnityVersion { get; set; }
            public bool IsPlaying { get; set; }
            public bool InPrefabIsolationMode { get; set; }
            public EditingPrefab? EditingPrefab { get; set; }
        }

        public struct EditingPrefab
        {
            public string Path { get; set; }
            public string Guid { get; set; }
        }

        [McpServerTool(
            Destructive = false,
            Idempotent = false,
            OpenWorld = false,
            ReadOnly = true,
            Title = "Get Unity State",
            Name = "get_state"
        )]
        [Description(
            "Get the state of the Unity Editor. Always call this tool as the first step in your workflow."
        )]
        internal static async Task<UnityState> GetState(
            SynchronizationContext context,
            CancellationToken cancellationToken
        )
        {
            return await context.Run(
                () =>
                {
                    int countLoaded = SceneManager.sceneCount;
                    Scene[] loadedScenes = new Scene[countLoaded];

                    for (int i = 0; i < countLoaded; i++)
                    {
                        loadedScenes[i] = SceneManager.GetSceneAt(i);
                    }

                    List<SceneService.SceneIndexEntry> openScenes =
                        new List<SceneService.SceneIndexEntry>();

                    foreach (var scene in loadedScenes)
                    {
                        openScenes.Add(
                            new SceneService.SceneIndexEntry()
                            {
                                Name = scene.name,
                                Path = scene.path,
                                Guid = AssetDatabase.AssetPathToGUID(scene.path),
                                BuildIndex = scene.buildIndex,
                                RootGameObjects = scene
                                    .GetRootGameObjects()
                                    .Select(g => g.name)
                                    .ToList(),
                            }
                        );
                    }

                    var prefabStage = StageUtility.GetCurrentStage() as PrefabStage;

                    return new UnityState()
                    {
                        OpenScenes = openScenes,
                        UnityVersion = Application.unityVersion,
                        IsPlaying = EditorApplication.isPlaying,
                        InPrefabIsolationMode = prefabStage != null,
                        EditingPrefab =
                            prefabStage != null
                                ? new()
                                {
                                    Path = prefabStage.prefabAssetPath,
                                    Guid = AssetDatabase.AssetPathToGUID(
                                        prefabStage.prefabAssetPath
                                    ),
                                }
                                : null,
                    };
                },
                cancellationToken
            );
        }
    }
}

#endif
