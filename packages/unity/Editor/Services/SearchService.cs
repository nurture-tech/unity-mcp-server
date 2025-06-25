#if !NO_MCP

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.Text.Json.Serialization;

namespace Nurture.MCP.Editor.Services
{
    [McpServerToolType]
    public static class SearchService
    {
        public record McPSearchResults : IPaginated
        {
            public List<McPSearchResultEntry> Entries { get; set; }
            public int NextCursor { get; set; }
            public bool HasMore => NextCursor > 0;
        }

        public record McPSearchResultEntry
        {
            public string Location { get; set; }
            public object Data { get; set; }
        }

        public record SearchResultGameObject
        {
            public string Name { get; set; }
            public string HierarchyPath { get; set; }
            public string ScenePath { get; set; }
            public bool IsInIsolatedPrefab { get; set; }
            public bool IsInLoadedScene { get; set; }
            public StateService.EditingPrefab? EditingPrefab { get; set; }
        }

        public record SearchResultUnityObject
        {
            public string Name { get; set; }
            public string Guid { get; set; }
            public string Type { get; set; }
            public string FileID { get; set; }
            public string Path { get; set; }
        }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum Location
        {
            [Description("Search assets and hierarchy.")]
            Everywhere = 0,

            [Description("Search only objects in loaded scenes or stages.")]
            Hierarchy = 1,

            [Description("Search only assets in the project.")]
            Project = 2,
        }

        [McpServerTool(
            Destructive = false,
            Idempotent = false,
            OpenWorld = false,
            ReadOnly = true,
            Title = "Search Unity Objects",
            Name = "search"
        )]
        [Description("Search project assets and scene objects.")]
        internal static async Task<McPSearchResults> Search(
            SynchronizationContext context,
            CancellationToken cancellationToken,
            IProgress<ProgressNotificationValue> progress,
            [Description(
                "The name of the object to search for. When searching by asset filename, exclude the file extension."
            )]
                string name = "",
            [Description(
                @"Unity search filters
                Valid examples: 
                    - `t:Texture` will find images/textures in the project or scene.
                    - `t:Camera` will find all gameobjects in the scene and/or prefabs in the project with a Camera component.
                    - `sprite:Tuna` will find all gameobjects containing components with a `sprite` property that refers to an asset named `Tuna`.
                    - `t:Mesh` will find models.
                    - `t:Mesh or t:Prefab` will find all assets that are models or prefabs.
                    - `t:Mesh` will find all meshes.
                    - `ref={t:Mesh}` will find all assets that reference a mesh.
                    - `ref={Assets/Prefabs/Tuna.prefab}` will find all assets that reference the Tuna prefab.
                Invalid examples:
                    - `t:Mesh t:Prefab` is invalid because an asset can't be two types."
            )]
                string filters = "",
            [Description("The cursor to start the search from.")] int cursor = 0,
            [Description("The location to search.")] Location location = Location.Everywhere
        )
        {
            return await context.Run(
                async () =>
                {
                    var databases = UnityEditor.Search.SearchService.EnumerateDatabases();

                    if (databases.Count() == 0)
                    {
                        throw new McpException(
                            "No search indexes found. Please create a search index."
                        );
                    }

                    var indexesReady = databases.All(db =>
                        UnityEditor.Search.SearchService.IsIndexReady(db.name)
                    );
                    var indexReadyAttempts = 5;

                    while (!indexesReady && indexReadyAttempts > 0)
                    {
                        progress.Report(
                            new ProgressNotificationValue()
                            {
                                Progress = 0.1f,
                                Message = "Waiting for search index to be ready...",
                                Total = 1.0f,
                            }
                        );
                        await Task.Delay(1000);
                        indexReadyAttempts--;
                        indexesReady = databases.All(db =>
                            UnityEditor.Search.SearchService.IsIndexReady(db.name)
                        );
                    }

                    if (!indexesReady)
                    {
                        throw new McpException(
                            "Search index is not ready yet. Please try again later."
                        );
                    }

                    var results = new List<McPSearchResultEntry>();

                    if (name == null && filters == null)
                    {
                        throw new McpException("No query or filters specified");
                    }

                    string query = name ?? "";
                    if (filters?.Length > 0)
                    {
                        query = $"({filters}) {query}";
                    }

                    switch (location)
                    {
                        case Location.Hierarchy:
                            query = $"h: {query}";
                            break;
                        case Location.Project:
                            query = $"p: {query}";
                            break;
                        case Location.Everywhere:
                            break;
                    }

                    bool completed = false;
                    IList<SearchItem> foundItems = null;
                    SearchContext searchContext = null;
                    bool hasMore = false;

                    UnityEditor.Search.SearchService.Request(
                        query,
                        (SearchContext context, IList<SearchItem> items) =>
                        {
                            completed = true;
                            searchContext = context;
                            // Only take the top 20 results for now
                            foundItems = items.Skip(cursor).Take(20).ToList();
                            hasMore = items.Count > cursor + 20;
                        },
                        SearchFlags.Sorted
                    );

                    while (!completed)
                    {
                        progress.Report(
                            new ProgressNotificationValue()
                            {
                                Progress = 0.5f,
                                Message = "Searching...",
                                Total = 1.0f,
                            }
                        );
                        await Task.Delay(100);
                    }

                    foreach (var result in foundItems)
                    {
                        if (result.provider.name == "Hierarchy")
                        {
                            GameObject obj =
                                result.provider.toObject(result, typeof(GameObject)) as GameObject;

                            var prefabStage = StageUtility.GetCurrentStage() as PrefabStage;
                            var isRootIsolatedPrefab =
                                prefabStage != null
                                && obj.transform.root == prefabStage.prefabContentsRoot.transform;

                            results.Add(
                                new McPSearchResultEntry()
                                {
                                    Location = result.provider.name,
                                    Data = new SearchResultGameObject()
                                    {
                                        Name = obj.name,
                                        HierarchyPath = SearchUtilsExtensions.GetTransformPath(
                                            obj.transform,
                                            isRootIsolatedPrefab
                                        ),
                                        ScenePath = obj.scene.path,
                                    },
                                }
                            );
                        }
                        else if (result.provider.name == "Project")
                        {
                            UnityEngine.Object obj = result.provider.toObject(
                                result,
                                typeof(UnityEngine.Object)
                            );

                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                                obj,
                                out var guid,
                                out var fileID
                            );

                            results.Add(
                                new McPSearchResultEntry()
                                {
                                    Location = result.provider.name,
                                    Data = new SearchResultUnityObject()
                                    {
                                        Name = obj.name,
                                        Guid = guid,
                                        FileID = fileID.ToString(),
                                        Type = obj.GetType().AssemblyQualifiedName,
                                        Path = AssetDatabase.GetAssetPath(obj),
                                    },
                                }
                            );
                        }
                        else
                        {
                            Debug.Log(result.provider.name);
                        }

                        // TODO: Add support for other providers
                    }

                    return new McPSearchResults()
                    {
                        Entries = results,
                        NextCursor = hasMore ? results.Count : -1,
                    };
                },
                cancellationToken
            );
        }
    }
}

#endif
