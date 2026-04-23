using System;
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

                var camera = canvas.worldCamera ? canvas.worldCamera : Camera.main;
                if (!camera)
                    return new ErrorResponse("capture_failed");

                var width = toolParams.GetInt("width", DefaultWidth).Value;
                var height = toolParams.GetInt("height", DefaultHeight).Value;
                var outputPath = CaptureUtility.ResolveOutputPath(toolParams.Get("output_path"), "ui-capture");

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
        }
    }
}
