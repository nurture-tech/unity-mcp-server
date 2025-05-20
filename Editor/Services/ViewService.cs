#if !NO_MCP

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;

namespace Nurture.MCP.Editor.Services
{
    [McpServerToolType]
    public static class ViewService
    {
        [McpServerTool(
            Destructive = false,
            Idempotent = true,
            OpenWorld = false,
            ReadOnly = false,
            Title = "Unity Focus on Game Object",
            Name = "focus_game_object"
        )]
        [Description("Focus on a game object in the scene view.")]
        internal static Task<string> FocusOnGameObject(
            SynchronizationContext context,
            CancellationToken cancellationToken,
            [Description("The path to the game object to focus on.")]
                string gameObjectHierarchyPath,
            [Description("Whether to hide all other game objects in the scene.")]
                bool isolated = false
        )
        {
            return context.Run(
                async () =>
                {
                    // Get the last active scene view
                    var sceneView =
                        SceneView.lastActiveSceneView
                        ?? throw new McpException("No active scene view found");

                    sceneView.Focus();

                    var gameObject =
                        GameObject.Find(gameObjectHierarchyPath)
                        ?? throw new McpException("Game object not found");

                    if (Selection.activeGameObject != gameObject)
                    {
                        Selection.activeGameObject = gameObject;
                    }

                    if (isolated)
                    {
                        SceneVisibilityManager.instance.Isolate(gameObject, true);
                    }

                    // Wait for the selection to be active
                    await Task.Delay(500);

                    // FIXME: Doing this twice focuses inside the object
                    sceneView.FrameSelected(false, true);

                    // Wait for focus to animate
                    await Task.Delay(500);

                    sceneView.Focus();

                    return $"Focused on {gameObjectHierarchyPath}";
                },
                cancellationToken
            );
        }

        [McpServerTool(
            Destructive = false,
            Idempotent = true,
            OpenWorld = false,
            ReadOnly = true,
            Title = "Unity Take Scene View Screenshot",
            Name = "screenshot"
        )]
        [Description(
            @"Retrieve a preview of what is focused in the scene view. 
            Only use this tool if the LLM model being used can interpret image data and the MCP client supports handling image content."
        )]
        internal static async Task<Content> TakeScreenshot(
            SynchronizationContext context,
            CancellationToken cancellationToken,
            [Description(
                "The path to the camera to render. If null, it will use the editor scene view camera."
            )]
                string cameraHierarchyPath = ""
        )
        {
            return await context.Run(
                async () =>
                {
                    string screenshotBase64 = null;
                    Camera camera = null;

                    if (cameraHierarchyPath?.Length > 0)
                    {
                        camera = GameObject.Find(cameraHierarchyPath)?.GetComponent<Camera>();

                        // If camera object is null or doesn't have a camera, just use the scene view camera
                        // Some LLMs hallucinate a value here because they can't help setting some argument
                    }

                    if (camera != null)
                    {
                        // Create a new texture with the scene view's dimensions
                        var texture = new Texture2D(
                            (int)camera.pixelRect.width,
                            (int)camera.pixelRect.height,
                            TextureFormat.RGB24,
                            false
                        );

                        RenderTexture renderTexture = RenderTexture.GetTemporary(
                            texture.width,
                            texture.height,
                            24
                        );

                        // Store the current render texture and set our new one
                        RenderTexture previousRenderTexture = camera.targetTexture;
                        camera.targetTexture = renderTexture;

                        // Render the scene view
                        camera.Render();

                        // Store the active render texture and set our new one
                        RenderTexture previousActiveTexture = RenderTexture.active;
                        RenderTexture.active = renderTexture;

                        // Read the pixels from the render texture to our texture
                        texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                        texture.Apply();

                        // Restore the previous render textures
                        camera.targetTexture = previousRenderTexture;
                        RenderTexture.active = previousActiveTexture;

                        // Clean up
                        RenderTexture.ReleaseTemporary(renderTexture);

                        // Convert the texture to base64
                        screenshotBase64 = texture.GetPngBase64();

                        UnityEngine.Object.DestroyImmediate(texture);
                    }
                    else
                    {
                        // Get the last active scene view
                        var sceneView =
                            SceneView.lastActiveSceneView
                            ?? throw new McpException("No active scene view found");

                        await EditorExtensions.FocusSceneView(cancellationToken);

                        int width = Mathf.RoundToInt(sceneView.position.width);
                        int height = Mathf.RoundToInt(sceneView.position.height);

                        if (width <= 0 || height <= 0)
                        {
                            throw new McpException(
                                $"Invalid Scene View dimensions: {width}x{height}"
                            );
                        }
                        Color[] pixels = UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(
                            sceneView.position.min,
                            width,
                            height
                        );

                        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                        texture.SetPixels(pixels);
                        texture.Apply();

                        screenshotBase64 = texture.GetPngBase64();

                        UnityEngine.Object.DestroyImmediate(texture);
                    }

                    return new Content()
                    {
                        Type = "image",
                        Data = screenshotBase64,
                        MimeType = "image/png",
                    };
                },
                cancellationToken
            );
        }
    }
}

#endif
