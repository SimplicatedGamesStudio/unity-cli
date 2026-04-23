using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityCliConnector.Tools
{
    [UnityCliTool(Name = "capture_scene_object", Description = "Capture an exact scene object subtree while hiding unrelated renderers.")]
    public static class CaptureSceneObject
    {
        const int DefaultWidth = 1920;
        const int DefaultHeight = 1080;

        public static object HandleCommand(JObject parameters)
        {
            var hiddenRenderers = new List<Renderer>();
            var hiddenCanvases = new List<Canvas>();

            try
            {
                var toolParams = new ToolParams(parameters ?? new JObject());
                var path = toolParams.GetRequired("path");
                if (!path.IsSuccess)
                    return new ErrorResponse(path.ErrorMessage);

                var target = GameObjectResolver.ResolveSceneObject(path.Value);
                var width = toolParams.GetInt("width", DefaultWidth).Value;
                var height = toolParams.GetInt("height", DefaultHeight).Value;
                var outputPath = CaptureUtility.ResolveOutputPath(toolParams.Get("output_path"), "scene-capture");

                var camera = Camera.main;
                if (!camera)
                {
#if UNITY_2023_1_OR_NEWER
                    camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
#else
                    camera = UnityEngine.Object.FindObjectOfType<Camera>();
#endif
                }

                if (!camera)
                    return new ErrorResponse("capture_failed");

                HideUnrelatedSceneContent(target, hiddenRenderers, hiddenCanvases);

                return CaptureUtility.CaptureCamera(camera, width, height, outputPath, new
                {
                    path = outputPath,
                    width,
                    height,
                    resolvedPath = GameObjectInfoSerializer.BuildResolvedPath(target, true),
                    mode = Application.isPlaying ? "play" : "edit",
                    source = "scene",
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message);
            }
            finally
            {
                foreach (var renderer in hiddenRenderers)
                    if (renderer)
                        renderer.enabled = true;

                foreach (var canvas in hiddenCanvases)
                    if (canvas)
                        canvas.enabled = true;
            }
        }

        static void HideUnrelatedSceneContent(GameObject target, List<Renderer> hiddenRenderers, List<Canvas> hiddenCanvases)
        {
            var targetTransforms = new HashSet<Transform>(target.GetComponentsInChildren<Transform>(true));

            foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!renderer.enabled)
                    continue;
                if (targetTransforms.Contains(renderer.transform))
                    continue;

                renderer.enabled = false;
                hiddenRenderers.Add(renderer);
            }

            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.enabled)
                    continue;
                if (targetTransforms.Contains(canvas.transform))
                    continue;

                canvas.enabled = false;
                hiddenCanvases.Add(canvas);
            }
        }
    }
}
