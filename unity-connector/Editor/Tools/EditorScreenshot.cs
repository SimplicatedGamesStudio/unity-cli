using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityCliConnector.Tools
{
    [UnityCliTool(Name = "screenshot", Description = "Capture a screenshot of the Unity editor. Views: scene, game.")]
    public static class EditorScreenshot
    {
        private const int DefaultWidth = 1920;
        private const int DefaultHeight = 1080;

        public class Parameters
        {
            [ToolParameter("View to capture: scene (default), game", Required = false)]
            public string View { get; set; }

            [ToolParameter("Override width (default 1920)", Required = false)]
            public int Width { get; set; }

            [ToolParameter("Override height (default 1080)", Required = false)]
            public int Height { get; set; }

            [ToolParameter("Output file path, absolute or relative to project root (default: Screenshots/screenshot.png)", Required = false)]
            public string OutputPath { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
                @params = new JObject();

            var p = new ToolParams(@params);
            var view = p.Get("view", "scene").ToLowerInvariant();
            var width = p.GetInt("width", DefaultWidth).Value;
            var height = p.GetInt("height", DefaultHeight).Value;
            var outputPath = CaptureUtility.ResolveOutputPath(
                p.Get("output_path", "Screenshots/screenshot.png"),
                "screenshot");

            try
            {
                switch (view)
                {
                    case "scene":
                        return CaptureSceneView(width, height, outputPath);
                    case "game":
                        return CaptureGameView(width, height, outputPath);
                    default:
                        return new ErrorResponse($"Unknown view '{view}'. Valid: scene, game.");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Screenshot failed: {e.Message}");
            }
        }

        private static object CaptureSceneView(int width, int height, string outputPath)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (!sceneView)
                return new ErrorResponse("No active SceneView found.");

            var camera = sceneView.camera;
            if (!camera)
                return new ErrorResponse("SceneView camera is null.");

            return CaptureUtility.CaptureCamera(camera, width, height, outputPath,
                new { path = outputPath, width, height });
        }

        private static object CaptureGameView(int width, int height, string outputPath)
        {
            var camera = Camera.main;
            if (!camera)
            {
#if UNITY_2023_1_OR_NEWER
                camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
#else
                camera = UnityEngine.Object.FindObjectOfType<Camera>();
#endif
                if (!camera)
                    return new ErrorResponse("No camera found in scene.");
            }

            return CaptureUtility.CaptureCamera(camera, width, height, outputPath,
                new { path = outputPath, width, height });
        }
    }
}
