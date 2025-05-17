#if !NO_MCP

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nurture.MCP.Editor.Services
{
    [McpServerToolType]
    public static class AssetService
    {
        public record AssetIndexEntry
        {
            public string Guid { get; set; }
            public string FileID { get; set; }
            public string Path { get; set; }
            public string Type { get; set; }
            public string Name { get; set; }
        }

        public record SelectedAssetEntry : AssetIndexEntry
        {
            public bool InActiveScene { get; set; }
            public string HierarchyPath { get; set; }
        }

        public struct CreatedAssetEntry
        {
            public JsonDocument ImporterSettings { get; set; }
            public string Guid { get; set; }
            public string Path { get; set; }
            public string Type { get; set; }
            public string Name { get; set; }
        }

        public struct TextureInfo
        {
            public string Name { get; set; }
            public MCPVector2 Size { get; set; }
        }

        public struct MeshInfo
        {
            public MCPBounds Bounds { get; set; }
            public string Name { get; set; }
            public int VertexCount { get; set; }
        }

        public struct AudioClipInfo
        {
            public string Name { get; set; }
            public int SampleRate { get; set; }
            public int Channels { get; set; }
            public float Length { get; set; }
        }

        [McpServerTool(
            Destructive = false,
            Idempotent = false,
            OpenWorld = false,
            ReadOnly = true,
            Title = "Get Asset Importer Settings",
            Name = "get_asset_importer"
        )]
        [Description("Get the importer settings for an asset.")]
        internal static Task<Content> GetAssetImporterContents(
            SynchronizationContext context,
            string guid,
            CancellationToken cancellationToken
        )
        {
            return context.Run(
                () =>
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);

                    var importer =
                        AssetImporter.GetAtPath(path)
                        ?? throw new McpException("Asset importer not found");
                    string data = EditorJsonUtility.ToJson(importer);

                    // Select the asset in the inspector automatically
                    var asset = AssetDatabase.LoadMainAssetAtPath(path);

                    Selection.activeObject = asset;

                    return new Content()
                    {
                        Type = "text",
                        Text = data,
                        MimeType = "application/json",
                    };
                },
                cancellationToken
            );
        }

        /*
        [McpServerTool(
            Destructive = false,
            Idempotent = false,
            OpenWorld = false,
            ReadOnly = true,
            Title = "Unpack Unity Asset",
            Name = "unpack_asset"
        )]
        [Description(
            @"Get a listing of the individual resources within an asset.
            This may include a bunch of gameobjects in a scene, a bunch of textures in a spritesheet, or a mesh and it's materials and animations.
            If you don't know what the guid is, use the `search` tool to find it."
        )]
        internal static Task<List<AssetIndexEntry>> UnpackAsset(
            SynchronizationContext context,
            string guid,
            CancellationToken cancellationToken
        )
        {
            return context.Run(
                () =>
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);

                    var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                    var result = new List<AssetIndexEntry>();

                    foreach (var asset in assets)
                    {
                        if (
                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                                asset,
                                out var guid,
                                out var fileID
                            )
                        )
                        {
                            result.Add(
                                new AssetIndexEntry()
                                {
                                    Guid = guid,
                                    FileID = fileID.ToString(),
                                    Path = path,
                                    Type = asset.GetType().AssemblyQualifiedName,
                                    Name = asset.name,
                                }
                            );
                        }
                    }

                    return result;
                },
                cancellationToken
            );
        }
        */

        [McpServerTool(
            Destructive = false,
            Idempotent = false,
            OpenWorld = false,
            ReadOnly = true,
            Title = "Get Asset Contents",
            Name = "get_asset_contents"
        )]
        [Description(
            @"Get the full contents of an asset or sub-asset.
            If you don't know what the guid or fileID is, use the `search` tool to find it."
        )]
        internal static Task<List<Content>> GetAssetContents(
            SynchronizationContext context,
            string guid,
            string fileID,
            CancellationToken cancellationToken, 
            IProgress<ProgressNotificationValue> progress
        )
        {
            // TODO: Make a bulk version of this that can get multiple assets at once

            return context.Run(
                () =>
                {
                    long fileIDLong = long.Parse(fileID);
                    string path = AssetDatabase.GUIDToAssetPath(guid);

                    var assets = AssetDatabase.LoadAllAssetsAtPath(path);

                    var asset =
                        assets.FirstOrDefault(a =>
                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                                a,
                                out _,
                                out var compareFileID
                            )
                            && compareFileID == fileIDLong
                        ) ?? throw new McpException("Asset not found");

                    // Select the asset in the inspector automaticall   y
                    Selection.activeObject = asset;

                    return asset switch
                    {
                        Texture2D texture => FormatTexture(texture),
                        Mesh mesh => FormatMesh(mesh),
                        GameObject gameObject => FormatGameObject(gameObject, progress, cancellationToken),
                        AudioClip audioClip => FormatAudioClip(audioClip),
                        // TODO: Add support for other asset types
                        _ => FormatAsset(asset),
                    };
                },
                cancellationToken
            );
        }

        private static Task<List<Content>> FormatAudioClip(AudioClip asset)
        {
            /*
            using var stream = new MemoryStream();
            Wav.Write(asset, stream);
            var base64 = Convert.ToBase64String(stream.ToArray());
            */
            return Task.FromResult(
                new List<Content>()
                {
                    new()
                    {
                        Type = "text",
                        Text = JsonSerializer.Serialize(
                            new AudioClipInfo()
                            {
                                Name = asset.name,
                                SampleRate = asset.frequency,
                                Channels = asset.channels,
                                Length = asset.length,
                            }
                        ),
                        MimeType = "application/json",
                    },
                    /*
                    new()
                    {
                        Type = "audio",
                        Data = base64,
                        MimeType = "audio/wav",
                    },
                    */
                }
            );
        }

        private static async Task<List<Content>> FormatGameObject(GameObject asset, IProgress<ProgressNotificationValue> progress, CancellationToken cancellationToken)
        {
            var assetPath = AssetDatabase.GetAssetPath(asset);
            var importer = AssetImporter.GetAtPath(assetPath);
            var result = await FormatAsset(asset);

            if (importer is ModelImporter)
            {
                var preview = AssetPreview.GetAssetPreview(asset);

                while (AssetPreview.IsLoadingAssetPreviews())
                {
                    progress.Report(new ProgressNotificationValue() { Progress = 0.5f, Message = "Loading asset preview..." });
                    await Task.Delay(100);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return new List<Content>();
                    }
                }


                if (preview != null)
                {
                    result.Add(
                        new Content()
                        {
                            Type = "image",
                            Data = preview.GetPngBase64(),
                            MimeType = "image/png",
                        }
                    );
                }
            }

            return result;
        }

        private static Task<List<Content>> FormatAsset(UnityEngine.Object asset)
        {
            string data = EditorJsonUtility.ToJson(asset);

            return Task.FromResult(
                new List<Content>()
                {
                    new()
                    {
                        Type = "text",
                        Text = data,
                        MimeType = "application/json",
                    },
                }
            );
        }

        private static Task<List<Content>> FormatTexture(Texture2D asset)
        {
            string base64 = asset.GetPngBase64();

            var textureInfo = new TextureInfo()
            {
                Name = asset.name,
                Size = new MCPVector2() { x = asset.width, y = asset.height },
            };

            return Task.FromResult(
                new List<Content>()
                {
                    new()
                    {
                        Type = "text",
                        Text = JsonSerializer.Serialize(textureInfo),
                        MimeType = "application/json",
                    },
                    new()
                    {
                        Type = "image",
                        Data = base64,
                        MimeType = "image/png",
                    },
                }
            );
        }

        private static async Task<List<Content>> FormatMesh(Mesh asset)
        {
            while (AssetPreview.IsLoadingAssetPreviews())
            {
                await Task.Delay(100);
            }

            var preview =
                AssetPreview.GetAssetPreview(asset)
                ?? throw new McpException("Failed to get asset preview");

            string data = JsonSerializer.Serialize(
                new MeshInfo()
                {
                    Bounds = asset.bounds,
                    Name = asset.name,
                    VertexCount = asset.vertexCount,
                }
            );

            string base64 = preview.GetPngBase64();

            return new List<Content>()
            {
                new()
                {
                    Type = "text",
                    Text = data,
                    MimeType = "application/json",
                },
                new()
                {
                    Type = "image",
                    Data = base64,
                    MimeType = "image/png",
                },
            };
        }

        [McpServerTool(
            Destructive = true,
            Idempotent = true,
            OpenWorld = false,
            ReadOnly = false,
            Title = "Copy Unity Asset.",
            Name = "copy_asset"
        )]
        [Description("Copy an asset to a new path.")]
        internal static Task<string> CopyAsset(
            SynchronizationContext context,
            IProgress<ProgressNotificationValue> progress,
            string oldPath,
            string newPath,
            CancellationToken cancellationToken
        )
        {
            return context.Run(
                async () =>
                {
                    await EditorExtensions.EnsureNotPlaying(progress, cancellationToken, 0.1f);
                    AssetDatabase.CopyAsset(oldPath, newPath);
                    return $"Successfully copied asset from {oldPath} to {newPath}";
                },
                cancellationToken
            );
        }

        [McpServerTool(
            Destructive = true,
            Idempotent = true,
            OpenWorld = false,
            ReadOnly = false,
            Title = "Import Asset into Unity",
            Name = "import_asset"
        )]
        [Description(
            @"Import an asset from the filesystem into Unity. 
            If the `dstPath` already exists, it will be overwritten.
            Returns the asset importer settings used."
        )]
        internal static async Task<CreatedAssetEntry> ImportAsset(
            SynchronizationContext context,
            IProgress<ProgressNotificationValue> progress,
            string srcPath,
            string dstPath,
            CancellationToken cancellationToken
        )
        {
            return await context.Run(
                async () =>
                {
                    await EditorExtensions.EnsureNotPlaying(progress, cancellationToken, 0.1f);

                    if (srcPath == dstPath)
                    {
                        throw new McpException("Source and destination paths are the same");
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(dstPath));

                    File.Copy(srcPath, dstPath, true);

                    AssetDatabase.ImportAsset(dstPath, ImportAssetOptions.Default);

                    var importer = AssetImporter.GetAtPath(dstPath);

                    JsonDocument importerSettings = JsonDocument.Parse(
                        EditorJsonUtility.ToJson(importer)
                    );

                    var asset = AssetDatabase.LoadMainAssetAtPath(dstPath);

                    // Select the asset in the inspector automatically
                    Selection.activeObject = asset;

                    return new CreatedAssetEntry()
                    {
                        Guid = AssetDatabase.AssetPathToGUID(dstPath),
                        Path = dstPath,
                        Type = asset.GetType().AssemblyQualifiedName,
                        Name = asset.name,
                        ImporterSettings = importerSettings,
                    };
                },
                cancellationToken
            );
        }

        [McpServerTool(
            Destructive = false,
            Idempotent = false,
            OpenWorld = false,
            ReadOnly = true,
            Title = "Get Selection",
            Name = "get_selection"
        )]
        [Description("Get the objects the user has currently selected in the editor.")]
        public static Task<List<SelectedAssetEntry>> GetSelection(
            SynchronizationContext context,
            CancellationToken cancellationToken
        )
        {
            return context.Run(
                () =>
                {
                    var selection = Selection.objects;

                    var result = new List<SelectedAssetEntry>();

                    foreach (var o in selection)
                    {
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                            o,
                            out var guid,
                            out var fileID
                        );

                        if (fileID == 0 && o is GameObject gameObject)
                        {
                            result.Add(
                                new SelectedAssetEntry()
                                {
                                    Type = o.GetType().AssemblyQualifiedName,
                                    Name = o.name,
                                    InActiveScene = true,
                                    HierarchyPath = SearchUtils.GetTransformPath(
                                        gameObject.transform
                                    ),
                                }
                            );
                        }
                        else
                        {
                            result.Add(
                                new SelectedAssetEntry()
                                {
                                    Guid = guid,
                                    FileID = fileID.ToString(),
                                    Path = AssetDatabase.GetAssetPath(o),
                                    Type = o.GetType().AssemblyQualifiedName,
                                    Name = o.name,
                                    InActiveScene = false,
                                }
                            );
                        }
                    }

                    return result;
                },
                cancellationToken
            );
        }
    }
}

#endif
