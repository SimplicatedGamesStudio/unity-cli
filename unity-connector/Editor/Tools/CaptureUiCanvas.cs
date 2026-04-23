using System;
using System.Collections.Generic;
using System.Linq;
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

                DisableUnrelatedSceneContent(clone, disabledCanvases, disabledRenderers);

                var captureCamera = ConfigureCaptureCamera(sourceCanvas, clone.GetComponent<Canvas>(), cameraObject, width, height);
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

        static Camera ConfigureCaptureCamera(Canvas sourceCanvas, Canvas cloneCanvas, GameObject cameraObject, int width, int height)
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.aspect = (float)width / height;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 1000f;

            if (sourceCanvas.renderMode == RenderMode.WorldSpace)
            {
                var referenceCamera = sourceCanvas.worldCamera ? sourceCanvas.worldCamera : Camera.main;
                if (!referenceCamera)
                    return null;

                PositionWorldSpaceCamera(camera, referenceCamera, cloneCanvas.GetComponent<RectTransform>(), width, height);
                return camera;
            }

            cloneCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            cloneCanvas.worldCamera = camera;
            cloneCanvas.planeDistance = 1f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
            return camera;
        }

        static void DisableUnrelatedSceneContent(GameObject cloneRoot, List<Canvas> disabledCanvases, List<Renderer> disabledRenderers)
        {
            var cloneTransforms = new HashSet<Transform>(cloneRoot.GetComponentsInChildren<Transform>(true));

            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.enabled)
                    continue;
                if (cloneTransforms.Contains(canvas.transform))
                    continue;

                canvas.enabled = false;
                disabledCanvases.Add(canvas);
            }

            foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!renderer.enabled)
                    continue;
                if (cloneTransforms.Contains(renderer.transform))
                    continue;

                renderer.enabled = false;
                disabledRenderers.Add(renderer);
            }
        }

        static void PositionWorldSpaceCamera(Camera camera, Camera referenceCamera, RectTransform rectTransform, int width, int height)
        {
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);

            var bounds = new Bounds(corners[0], Vector3.zero);
            for (var index = 1; index < corners.Length; index++)
                bounds.Encapsulate(corners[index]);

            var aspect = (float)width / height;
            var center = bounds.center;
            var direction = center - referenceCamera.transform.position;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
                direction = rectTransform.forward;
            direction.Normalize();

            camera.orthographic = true;
            camera.transform.position = center - direction * 10f;
            camera.transform.rotation = Quaternion.LookRotation(direction, referenceCamera.transform.up);

            var horizontalExtent = 0f;
            var verticalExtent = 0f;
            foreach (var corner in corners)
            {
                var offset = corner - center;
                horizontalExtent = Mathf.Max(horizontalExtent, Mathf.Abs(Vector3.Dot(offset, camera.transform.right)));
                verticalExtent = Mathf.Max(verticalExtent, Mathf.Abs(Vector3.Dot(offset, camera.transform.up)));
            }

            camera.orthographicSize = Mathf.Max(verticalExtent, horizontalExtent / aspect) + 0.1f;
        }
    }
}
