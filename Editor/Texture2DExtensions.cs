using System;
using UnityEngine;

namespace Nurture.MCP.Editor
{
    public static class Texture2DExtensions
    {
        public static string GetPngBase64(this Texture2D texture)
        {
            // Create a temporary RenderTexture and copy the texture content to it
            RenderTexture tempRT = RenderTexture.GetTemporary(
                texture.width,
                texture.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Linear
            );

            // Copy the source texture to the temporary RenderTexture
            Graphics.Blit(texture, tempRT);

            // Store the active RenderTexture
            RenderTexture previousRT = RenderTexture.active;

            // Set the temporary RenderTexture as active
            RenderTexture.active = tempRT;

            // Create a new readable texture
            Texture2D readableTexture = new Texture2D(texture.width, texture.height);

            // Read pixels from the active RenderTexture (tempRT) into the readable texture
            readableTexture.ReadPixels(new Rect(0, 0, tempRT.width, tempRT.height), 0, 0);
            readableTexture.Apply();

            // Restore the previously active RenderTexture
            RenderTexture.active = previousRT;

            // Release the temporary RenderTexture
            RenderTexture.ReleaseTemporary(tempRT);

            // Use the readable texture to create the base64 string
            string base64 = Convert.ToBase64String(readableTexture.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(readableTexture);

            return base64;
        }
    }
}