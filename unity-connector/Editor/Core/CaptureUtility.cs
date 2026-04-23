using System;
using System.IO;
using UnityEngine;

namespace UnityCliConnector
{
    public static class CaptureUtility
    {
        public static string ResolveOutputPath(string userPath, string prefix)
        {
            if (string.IsNullOrEmpty(userPath))
                userPath = $"Screenshots/{prefix}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.png";

            if (Path.IsPathRooted(userPath))
                return Path.GetFullPath(userPath);

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.GetFullPath(Path.Combine(projectRoot, userPath));
        }

        public static object CaptureCamera(Camera camera, int width, int height, string outputPath, object data)
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var previousRenderTexture = camera.targetTexture;
            RenderTexture renderTexture = null;
            Texture2D texture = null;

            try
            {
                renderTexture = new RenderTexture(width, height, 24);
                camera.targetTexture = renderTexture;
                camera.Render();

                RenderTexture.active = renderTexture;
                texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();

                File.WriteAllBytes(outputPath, texture.EncodeToPNG());
                return new SuccessResponse($"Screenshot saved to {outputPath}", data);
            }
            finally
            {
                camera.targetTexture = previousRenderTexture;
                RenderTexture.active = null;
                if (renderTexture)
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                if (texture)
                    UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
