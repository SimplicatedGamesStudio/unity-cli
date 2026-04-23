using System;
using System.Collections.Generic;
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
            GameObject cameraObject = null;

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

                cameraObject = CreateCaptureCamera(target, width, height);
                var camera = cameraObject ? cameraObject.GetComponent<Camera>() : null;
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

                if (cameraObject)
                    UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        static void HideUnrelatedSceneContent(GameObject target, List<Renderer> hiddenRenderers, List<Canvas> hiddenCanvases)
        {
            var targetTransforms = new HashSet<Transform>(target.GetComponentsInChildren<Transform>(true));
            for (var current = target.transform.parent; current != null; current = current.parent)
                targetTransforms.Add(current);

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

        static GameObject CreateCaptureCamera(GameObject target, int width, int height)
        {
            var referenceCamera = Camera.main;
            if (!referenceCamera)
            {
#if UNITY_2023_1_OR_NEWER
                referenceCamera = UnityEngine.Object.FindFirstObjectByType<Camera>();
#else
                referenceCamera = UnityEngine.Object.FindObjectOfType<Camera>();
#endif
            }

            if (!referenceCamera)
                return null;

            var cameraObject = new GameObject("__SceneCaptureCamera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.aspect = (float)width / height;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 1000f;

            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                camera.transform.position = referenceCamera.transform.position;
                camera.transform.rotation = referenceCamera.transform.rotation;
                return cameraObject;
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);

            var center = bounds.center;
            var direction = center - referenceCamera.transform.position;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
                direction = target.transform.forward;
            direction.Normalize();

            camera.orthographic = true;
            camera.transform.position = center - direction * 10f;
            camera.transform.rotation = Quaternion.LookRotation(direction, referenceCamera.transform.up);

            var horizontalExtent = 0f;
            var verticalExtent = 0f;
            foreach (var renderer in renderers)
            {
                foreach (var corner in GetBoundsCorners(renderer.bounds))
                {
                    var offset = corner - center;
                    horizontalExtent = Mathf.Max(horizontalExtent, Mathf.Abs(Vector3.Dot(offset, camera.transform.right)));
                    verticalExtent = Mathf.Max(verticalExtent, Mathf.Abs(Vector3.Dot(offset, camera.transform.up)));
                }
            }

            camera.orthographicSize = Mathf.Max(verticalExtent, horizontalExtent / camera.aspect) + 0.1f;
            return cameraObject;
        }

        static Vector3[] GetBoundsCorners(Bounds bounds)
        {
            var min = bounds.min;
            var max = bounds.max;
            return new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z),
            };
        }
    }
}
