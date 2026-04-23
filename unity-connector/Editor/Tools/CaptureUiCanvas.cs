using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityCliConnector.Tools
{
    [UnityCliTool(Name = "capture_ui_canvas", Description = "Capture an exact Canvas object from the loaded scene.")]
    public static class CaptureUiCanvas
    {
        const int DefaultWidth = 1920;
        const int DefaultHeight = 1080;

        public static object HandleCommand(JObject parameters)
        {
            try
            {
                var toolParams = new ToolParams(parameters ?? new JObject());
                var path = toolParams.GetRequired("path");
                if (!path.IsSuccess)
                    return new ErrorResponse(path.ErrorMessage);

                var target = GameObjectResolver.ResolveSceneObject(path.Value);
                var canvas = target.GetComponent<Canvas>();
                if (!canvas)
                    return new ErrorResponse("invalid_target_type");

                var width = toolParams.GetInt("width", DefaultWidth).Value;
                var height = toolParams.GetInt("height", DefaultHeight).Value;
                var outputPath = CaptureUtility.ResolveOutputPath(toolParams.Get("output_path"), "ui-capture");
                return CaptureIsolatedCanvas(target, canvas, width, height, outputPath);
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message);
            }
        }

        static object CaptureIsolatedCanvas(GameObject target, Canvas sourceCanvas, int width, int height, string outputPath)
        {
            GameObject clone = null;
            GameObject cameraObject = null;
            var disabledCanvases = new List<Canvas>();
            var disabledRenderers = new List<Renderer>();

            try
            {
                clone = UnityEngine.Object.Instantiate(target);
                clone.name = target.name;
                clone.hideFlags = HideFlags.HideAndDontSave;
                clone.transform.SetParent(null, false);

                cameraObject = new GameObject("__UiCaptureCamera");
                cameraObject.hideFlags = HideFlags.HideAndDontSave;

                UiCaptureUtility.DisableUnrelatedSceneContent(clone, disabledCanvases, disabledRenderers);

                var captureCamera = UiCaptureUtility.ConfigureCaptureCamera(sourceCanvas, clone.GetComponent<Canvas>(), cameraObject, width, height);
                if (!captureCamera)
                    return new ErrorResponse("capture_failed");

                Canvas.ForceUpdateCanvases();

                return CaptureUtility.CaptureCamera(captureCamera, width, height, outputPath, new
                {
                    path = outputPath,
                    width,
                    height,
                    resolvedPath = GameObjectInfoSerializer.BuildResolvedPath(target, true),
                    mode = Application.isPlaying ? "play" : "edit",
                    source = "scene",
                });
            }
            finally
            {
                foreach (var renderer in disabledRenderers)
                    if (renderer)
                        renderer.enabled = true;

                foreach (var canvas in disabledCanvases)
                    if (canvas)
                        canvas.enabled = true;

                if (cameraObject)
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                if (clone)
                    UnityEngine.Object.DestroyImmediate(clone);
            }
        }

    }
}
