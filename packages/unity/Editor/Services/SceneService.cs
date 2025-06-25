#if !NO_MCP

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nurture.MCP.Editor.Services
{
    [McpServerToolType]
    public static class SceneService
    {
        public struct Optional<T>
        {
            public bool Exists { get; set; }
            public T Value { get; set; }
        }

        public struct SceneIndexEntry
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string Guid { get; set; }
            public int BuildIndex { get; set; }
            public List<string> RootGameObjects { get; set; }
        }

        public struct GameObjectData
        {
            public bool ActiveSelf { get; set; }
            public bool ActiveInHierarchy { get; set; }
            public string HierarchyPath { get; set; }
            public List<ComponentData> Components { get; set; }
            public bool IsPartOfPrefab { get; set; }
            public string PrefabRoot { get; set; }

            public List<GameObjectData> Children { get; set; }
        }

        public struct ComponentData
        {
            public string Type { get; set; }
            public JsonDocument Data { get; set; }
        }

        [McpServerTool(
            Destructive = false,
            Idempotent = true,
            OpenWorld = false,
            ReadOnly = true,
            Title = "Open Unity Scene",
            Name = "open_scene"
        )]
        [Description(
            "Open a scene by its GUID.  If the scene is already open, nothing will happen. If the current scene has unsaved changes, this will throw an exception."
        )]
        internal static async Task<SceneIndexEntry> OpenScene(
            SynchronizationContext context,
            IProgress<ProgressNotificationValue> progress,
            [Description(
                "The GUID of the scene to load. Use `search` tool to find the guid if you don't know it."
            )]
                string guid,
            [Description("The mode to open the scene in.")] OpenSceneMode mode,
            CancellationToken cancellationToken
        )
        {
            return await context.Run(
                async () =>
                {
                    await EditorExtensions.EnsureNotPlaying(progress, cancellationToken, 0.1f);

                    string path =
                        AssetDatabase.GUIDToAssetPath(guid)
                        ?? throw new McpException("The guid does not exist.");
                    Scene activeScene = EditorSceneManager.GetActiveScene();

                    if (activeScene.path == path)
                    {
                        // The scene is already loaded
                        return new SceneIndexEntry()
                        {
                            Name = activeScene.name,
                            Path = activeScene.path,
                            Guid = AssetDatabase.AssetPathToGUID(activeScene.path),
                            BuildIndex = activeScene.buildIndex,
                        };
                    }

                    if (mode == OpenSceneMode.Single && activeScene.isDirty)
                    {
                        throw new McpException(
                            "The active scene is dirty. Please save it or discard changes before loading another scene."
                        );
                    }

                    Scene scene = EditorSceneManager.OpenScene(path, mode);

                    // Get the last active scene view
                    var sceneView =
                        SceneView.lastActiveSceneView
                        ?? throw new McpException("No active scene view found");

                    sceneView.Focus();

                    return new SceneIndexEntry()
                    {
                        Name = scene.name,
                        Path = scene.path,
                        Guid = AssetDatabase.AssetPathToGUID(scene.path),
                        BuildIndex = scene.buildIndex,
                        RootGameObjects = scene.GetRootGameObjects().Select(g => g.name).ToList(),
                    };
                },
                cancellationToken
            );
        }

        [McpServerTool(
            Destructive = false,
            Idempotent = true,
            OpenWorld = false,
            ReadOnly = false,
            Title = "Close Unity Scene",
            Name = "close_scene"
        )]
        [Description(
            "Close a scene by its GUID. If the scene is not open or this is the only open scene, this will do nothing. If the scene has unsaved changes, this will throw an exception."
        )]
        internal static async Task<string> CloseScene(
            SynchronizationContext context,
            IProgress<ProgressNotificationValue> progress,
            [Description(
                "The GUID of the scene to close. Use the `get_state` tool to find the guid if you don't know it."
            )]
                string guid,
            CancellationToken cancellationToken
        )
        {
            return await context.Run(
                async () =>
                {
                    await EditorExtensions.EnsureNotPlaying(progress, cancellationToken, 0.1f);

                    string path =
                        AssetDatabase.GUIDToAssetPath(guid)
                        ?? throw new McpException("The guid does not exist.");

                    Scene scene = EditorSceneManager.GetSceneByPath(path);

                    if (!scene.IsValid())
                    {
                        throw new McpException("The scene is not loaded.");
                    }

                    if (scene.isDirty)
                    {
                        throw new McpException(
                            "The scene is dirty. Please save it or discard changes before unloading."
                        );
                    }

                    EditorSceneManager.CloseScene(scene, true);

                    // Get the last active scene view
                    var sceneView =
                        SceneView.lastActiveSceneView
                        ?? throw new McpException("No active scene view found");

                    sceneView.Focus();

                    return "The scene was closed successfully.";
                },
                cancellationToken
            );
        }

        [McpServerTool(
            Destructive = true,
            Idempotent = true,
            OpenWorld = false,
            ReadOnly = false,
            Title = "Save Unity Scene",
            Name = "save_scene"
        )]
        [Description("Save the current scene. If the scene is not dirty, this will do nothing.")]
        internal static async Task<string> SaveScene(
            SynchronizationContext context,
            IProgress<ProgressNotificationValue> progress,
            CancellationToken cancellationToken
        )
        {
            return await context.Run(
                async () =>
                {
                    await EditorExtensions.EnsureNotPlaying(progress, cancellationToken, 0.1f);

                    EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

                    await EditorExtensions.FocusSceneView(cancellationToken);

                    return "The scene was saved successfully.";
                },
                cancellationToken
            );
        }

        [McpServerTool(
            Destructive = false,
            Idempotent = false,
            OpenWorld = false,
            ReadOnly = true,
            Title = "Get GameObject in Active Scenes",
            Name = "get_game_object"
        )]
        [Description(
            "Get the details of a game object in a loaded scene or prefab by its hierarchy path."
        )]
        internal static async Task<GameObjectData> GetGameObject(
            SynchronizationContext context,
            CancellationToken cancellationToken,
            string hierarchyPath,
            [Description("Whether to return the components of the game object.")]
                bool expandComponents,
            [Description("Whether to return the children of the game object.")] bool expandChildren,
            [Description(
                "Whether to search for the game object in the prefab open in isolation mode."
            )]
                bool searchIsolatedPrefab
        )
        {
            return await context.Run(
                () =>
                {
                    GameObject go = null;

                    if (searchIsolatedPrefab)
                    {
                        var stage = StageUtility.GetCurrentStage() as PrefabStage;

                        if (stage == null)
                        {
                            throw new McpException("No prefab is open in isolation mode.");
                        }

                        if (hierarchyPath == "/")
                        {
                            go = stage.prefabContentsRoot.gameObject;
                        }
                        else
                        {
                            var prefab = stage.prefabContentsRoot;
                            go = prefab.transform.Find(hierarchyPath.Substring(1))?.gameObject;
                        }
                    }
                    else
                    {
                        go = GameObject.Find(hierarchyPath);
                    }

                    if (go == null)
                    {
                        throw new McpException("The game object is not found.");
                    }

                    return SerializeGameObject(go, expandComponents, expandChildren);
                },
                cancellationToken
            );
        }

        [McpServerTool(
            Destructive = false,
            Idempotent = true,
            OpenWorld = false,
            ReadOnly = false,
            Title = "Test Active Scene",
            Name = "test_active_scene"
        )]
        [Description(
            "Test the active scene by entering play mode and running for a given number of seconds."
        )]
        internal static Task<List<ContentBlock>> TestActiveScene(
            SynchronizationContext context,
            IProgress<ProgressNotificationValue> progress,
            CancellationToken cancellationToken,
            [Description("The number of seconds to run the scene.")] int secondsToRun = 5,
            [Description(@"If true, take a screenshot every second.")] bool takeScreenshots = false
        )
        {
            return context.Run(
                async () =>
                {
                    await EditorExtensions.EnsureNotPlaying(progress, cancellationToken, 0.1f);

                    Scene activeScene = SceneManager.GetActiveScene();
                    if (activeScene.isDirty)
                    {
                        throw new McpException(
                            "The active scene is dirty. Please save it or discard changes before testing."
                        );
                    }

                    List<string> screenshots = new List<string>();

                    var result = await UnityLoggerExtensions.WithLogs(async () =>
                    {
                        // Start play mode
                        await EditorExtensions.FocusSceneView(cancellationToken);

                        // Don't reload assemblies when entering play mode as that will break the MCP connection
                        var savedEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
                        EditorSettings.enterPlayModeOptions =
                            EnterPlayModeOptions.DisableDomainReload;
                        EditorApplication.isPlaying = true;

                        // Wait for play mode to fully start
                        while (!EditorApplication.isPlaying)
                        {
                            progress.Report(
                                new ProgressNotificationValue()
                                {
                                    Message = "Waiting for play mode to start...",
                                    Progress = 0.5f,
                                    Total = 1.0f,
                                }
                            );
                            await Task.Delay(100);
                        }

                        float expires = Time.time + secondsToRun;

                        // Play for run time
                        while (Time.time < expires && EditorApplication.isPlaying)
                        {
                            progress.Report(
                                new ProgressNotificationValue()
                                {
                                    Message = $"Running for {secondsToRun} seconds...",
                                    Progress =
                                        0.5f
                                        + (0.4f * ((expires - Time.time) / (float)secondsToRun)),
                                    Total = 1.0f,
                                }
                            );

                            await Task.Delay(1000);

                            // Take a screenshot
                            if (takeScreenshots)
                            {
                                await Awaitable.EndOfFrameAsync();
                                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                                screenshots.Add(texture.GetPngBase64());
                                UnityEngine.Object.Destroy(texture);
                            }
                        }

                        EditorApplication.isPlaying = false;

                        while (EditorApplication.isPlaying)
                        {
                            progress.Report(
                                new ProgressNotificationValue()
                                {
                                    Message = "Waiting for play mode to end...",
                                    Progress = 0.9f,
                                    Total = 1.0f,
                                }
                            );
                            await Task.Delay(100);
                        }
                        EditorSettings.enterPlayModeOptions = savedEnterPlayModeOptions;
                    });

                    var msg = $"Log messages: \n{result}.";

                    if (screenshots.Count > 0)
                    {
                        msg += $"\n\nScreenshots are attached.";
                    }

                    var results = new List<ContentBlock>
                    {
                        new TextContentBlock()
                        {
                            Text = msg,
                        },
                    };

                    foreach (var screenshot in screenshots)
                    {
                        results.Add(
                            new ImageContentBlock() { Data = screenshot, MimeType = "image/png" }
                        );
                    }

                    return results;
                },
                cancellationToken
            );
        }

        private static GameObjectData SerializeGameObject(
            GameObject go,
            bool expandComponents,
            bool expandChildren
        )
        {
            bool isPrefab = PrefabUtility.IsPartOfAnyPrefab(go);
            var prefabStage = StageUtility.GetCurrentStage() as PrefabStage;
            var isRootIsolatedPrefab =
                prefabStage != null
                && go.transform.root == prefabStage.prefabContentsRoot.transform;

            string prefabRoot = isPrefab
                ? SearchUtilsExtensions.GetTransformPath(
                    PrefabUtility.GetNearestPrefabInstanceRoot(go).transform,
                    isRootIsolatedPrefab
                )
                : null;

            return new GameObjectData()
            {
                ActiveSelf = go.activeSelf,
                ActiveInHierarchy = go.activeInHierarchy,
                IsPartOfPrefab = isPrefab,
                PrefabRoot = prefabRoot,
                HierarchyPath = SearchUtilsExtensions.GetTransformPath(
                    go.transform,
                    isRootIsolatedPrefab
                ),
                Components = expandComponents
                    ? go.GetComponents<UnityEngine.Component>().Select(SerializeComponent).ToList()
                    : null,
                Children = expandChildren
                    ? GetChildren(go).Select((g) => SerializeGameObject(g, false, false)).ToList()
                    : null,
            };
        }

        private static List<GameObject> GetChildren(GameObject go)
        {
            List<GameObject> children = new List<GameObject>();
            foreach (Transform child in go.transform)
            {
                children.Add(child.gameObject);
            }
            return children;
        }

        private static ComponentData SerializeComponent(UnityEngine.Component component)
        {
            return new ComponentData()
            {
                Type = component.GetType().AssemblyQualifiedName,
                Data = JsonDocument.Parse(EditorJsonUtility.ToJson(component)),
            };
        }
    }
}

#endif
