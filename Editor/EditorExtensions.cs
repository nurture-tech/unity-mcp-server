#if USE_MCP

using System;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using UnityEditor;

namespace Nurture.MCP.Editor
{
    public static class EditorExtensions
    {
        public static async Task EnsureNotPlaying(
            IProgress<ProgressNotificationValue> progress,
            CancellationToken cancellationToken,
            float progressValue
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check to make sure we are not in play mode
            if (EditorApplication.isPlaying)
            {
                // Prevent exit play mode reloading assemblies and restarting the mcp server
                EditorApplication.LockReloadAssemblies();

                try
                {
                    EditorApplication.ExitPlaymode();

                    while (EditorApplication.isPlaying)
                    {
                        progress.Report(
                            new ProgressNotificationValue()
                            {
                                Message = "Waiting for play mode to end...",
                                Progress = progressValue,
                                Total = 1.0f,
                            }
                        );
                        await Task.Delay(100, cancellationToken);
                    }
                }
                finally
                {
                    EditorApplication.UnlockReloadAssemblies();
                }
            }
        }

        public static async Task FocusSceneView(CancellationToken cancellationToken)
        {
            var sceneView =
                SceneView.lastActiveSceneView ?? throw new McpException("No active scene view");
            sceneView.Focus();
            await Task.Delay(500, cancellationToken);
        }
    }
}

#endif
