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
            return CaptureCamera(camera, width, height, outputPath, data, null);
        }

        public static object CaptureCameraToVisibleBounds(Camera camera, int width, int height, string outputPath, object data)
        {
            return CaptureCamera(camera, width, height, outputPath, data, null, true);
        }

        public static object CaptureCamera(Camera camera, int width, int height, string outputPath, object data, RectInt? cropRect)
        {
            return CaptureCamera(camera, width, height, outputPath, data, cropRect, false);
        }

        static object CaptureCamera(Camera camera, int width, int height, string outputPath, object data, RectInt? cropRect, bool cropVisiblePixels)
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            Texture2D texture = null;
            Texture2D outputTexture = null;

            try
            {
                texture = RenderCamera(camera, width, height);
                var effectiveCropRect = cropRect;
                if (!effectiveCropRect.HasValue && cropVisiblePixels)
                    effectiveCropRect = CalculateVisiblePixelBounds(texture);

                if (cropVisiblePixels && !effectiveCropRect.HasValue)
                    return new ErrorResponse("capture_failed");

                outputTexture = effectiveCropRect.HasValue ? CropAndResize(texture, effectiveCropRect.Value, width, height) : texture;
                File.WriteAllBytes(outputPath, outputTexture.EncodeToPNG());
                return new SuccessResponse($"Screenshot saved to {outputPath}", data);
            }
            finally
            {
                if (outputTexture && outputTexture != texture)
                    UnityEngine.Object.DestroyImmediate(outputTexture);
                if (texture)
                    UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static Texture2D RenderCamera(Camera camera, int width, int height)
        {
            var previousRenderTexture = camera.targetTexture;
            var previousActive = RenderTexture.active;
            RenderTexture renderTexture = null;

            try
            {
                renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = renderTexture;
                camera.Render();

                RenderTexture.active = renderTexture;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                return texture;
            }
            finally
            {
                camera.targetTexture = previousRenderTexture;
                RenderTexture.active = previousActive;
                if (renderTexture)
                    UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        static RectInt? CalculateVisiblePixelBounds(Texture2D source)
        {
            var pixels = source.GetPixels32();
            var minX = source.width;
            var minY = source.height;
            var maxX = -1;
            var maxY = -1;

            for (var y = 0; y < source.height; y++)
            {
                var rowOffset = y * source.width;
                for (var x = 0; x < source.width; x++)
                {
                    if (pixels[rowOffset + x].a == 0)
                        continue;

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
                return null;

            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        static Texture2D CropAndResize(Texture2D source, RectInt cropRect, int width, int height)
        {
            var normalizedX = Mathf.Clamp(cropRect.x, 0, source.width - 1);
            var normalizedY = Mathf.Clamp(cropRect.y, 0, source.height - 1);
            var normalizedWidth = Mathf.Clamp(cropRect.width, 1, source.width - normalizedX);
            var normalizedHeight = Mathf.Clamp(cropRect.height, 1, source.height - normalizedY);

            var croppedTexture = new Texture2D(normalizedWidth, normalizedHeight, TextureFormat.RGBA32, false);
            croppedTexture.SetPixels(source.GetPixels(normalizedX, normalizedY, normalizedWidth, normalizedHeight));
            croppedTexture.Apply();

            if (normalizedWidth == width && normalizedHeight == height)
                return croppedTexture;

            var previousActive = RenderTexture.active;
            RenderTexture temporary = null;
            Texture2D resizedTexture = null;

            try
            {
                temporary = RenderTexture.GetTemporary(width, height, 0);
                Graphics.Blit(croppedTexture, temporary);
                RenderTexture.active = temporary;

                resizedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                resizedTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                resizedTexture.Apply();
                return resizedTexture;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (temporary)
                    RenderTexture.ReleaseTemporary(temporary);
                if (croppedTexture && croppedTexture != resizedTexture)
                    UnityEngine.Object.DestroyImmediate(croppedTexture);
            }
        }
    }
}
