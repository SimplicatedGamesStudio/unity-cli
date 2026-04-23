using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

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
            var hiddenGraphics = new List<Graphic>();
            GameObject cameraObject = null;
            Canvas canvasOverride = null;
            RenderMode originalRenderMode = default;
            Camera originalWorldCamera = null;
            float originalPlaneDistance = 0f;

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

                var parentCanvas = target.GetComponentInParent<Canvas>();
                if (ShouldCaptureAsUiSubtree(target, parentCanvas))
                    return CaptureUiSubtree(target, parentCanvas, width, height, outputPath);

                cameraObject = CreateCaptureCamera(target, parentCanvas, width, height,
                    out canvasOverride, out originalRenderMode, out originalWorldCamera, out originalPlaneDistance);
                var camera = cameraObject ? cameraObject.GetComponent<Camera>() : null;
                if (!camera)
                    return new ErrorResponse("capture_failed");

                HideUnrelatedSceneContent(target, hiddenRenderers, hiddenCanvases, hiddenGraphics);

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

                foreach (var graphic in hiddenGraphics)
                    if (graphic)
                        graphic.enabled = true;

                if (canvasOverride)
                {
                    canvasOverride.renderMode = originalRenderMode;
                    canvasOverride.worldCamera = originalWorldCamera;
                    canvasOverride.planeDistance = originalPlaneDistance;
                }

                if (cameraObject)
                    UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        static object CaptureUiSubtree(GameObject target, Canvas sourceCanvas, int width, int height, string outputPath)
        {
            GameObject cloneRoot = null;
            GameObject cloneTarget = null;
            GameObject cameraObject = null;
            var disabledCanvases = new List<Canvas>();
            var disabledRenderers = new List<Renderer>();

            try
            {
                cloneRoot = UnityEngine.Object.Instantiate(sourceCanvas.gameObject);
                cloneRoot.name = sourceCanvas.gameObject.name;
                cloneRoot.hideFlags = HideFlags.HideAndDontSave;
                cloneRoot.transform.SetParent(null, false);

                cloneTarget = ResolveCloneTarget(sourceCanvas.transform, target.transform, cloneRoot.transform);
                HideCloneContentOutsideTarget(cloneRoot, cloneTarget);

                cameraObject = new GameObject("__SceneCaptureUiCamera");
                cameraObject.hideFlags = HideFlags.HideAndDontSave;

                UiCaptureUtility.DisableUnrelatedSceneContent(cloneRoot, disabledCanvases, disabledRenderers);
                var captureCamera = UiCaptureUtility.ConfigureCaptureCamera(sourceCanvas, cloneRoot.GetComponent<Canvas>(), cameraObject, width, height);
                if (!captureCamera)
                    return new ErrorResponse("capture_failed");

                Canvas.ForceUpdateCanvases();
                return CaptureUtility.CaptureCameraToVisibleBounds(captureCamera, width, height, outputPath, new
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
                if (cloneRoot)
                    UnityEngine.Object.DestroyImmediate(cloneRoot);
            }
        }

        static void HideUnrelatedSceneContent(GameObject target, List<Renderer> hiddenRenderers, List<Canvas> hiddenCanvases, List<Graphic> hiddenGraphics)
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

            foreach (var graphic in UnityEngine.Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None))
            {
                if (!graphic.enabled)
                    continue;
                if (targetTransforms.Contains(graphic.transform))
                    continue;

                graphic.enabled = false;
                hiddenGraphics.Add(graphic);
            }
        }

        static void HideCloneContentOutsideTarget(GameObject cloneRoot, GameObject cloneTarget)
        {
            var visibleTransforms = new HashSet<Transform>(cloneTarget.GetComponentsInChildren<Transform>(true));

            foreach (var graphic in cloneRoot.GetComponentsInChildren<Graphic>(true))
            {
                if (!graphic.enabled)
                    continue;
                if (visibleTransforms.Contains(graphic.transform))
                    continue;

                graphic.enabled = false;
            }

            foreach (var renderer in cloneRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled)
                    continue;
                if (visibleTransforms.Contains(renderer.transform))
                    continue;

                renderer.enabled = false;
            }
        }

        static bool ShouldCaptureAsUiSubtree(GameObject target, Canvas parentCanvas)
        {
            if (!parentCanvas)
                return false;

            return target.GetComponentsInChildren<Renderer>(true).Length == 0 &&
                   target.GetComponentsInChildren<RectTransform>(true).Length > 0;
        }

        static GameObject ResolveCloneTarget(Transform sourceRoot, Transform sourceTarget, Transform cloneRoot)
        {
            if (sourceTarget == sourceRoot)
                return cloneRoot.gameObject;

            var siblingIndices = new Stack<int>();
            for (var current = sourceTarget; current != null && current != sourceRoot; current = current.parent)
                siblingIndices.Push(current.GetSiblingIndex());

            var cloneCurrent = cloneRoot;
            while (siblingIndices.Count > 0)
            {
                var childIndex = siblingIndices.Pop();
                if (childIndex < 0 || childIndex >= cloneCurrent.childCount)
                    throw new InvalidOperationException("capture_failed");

                cloneCurrent = cloneCurrent.GetChild(childIndex);
            }

            return cloneCurrent.gameObject;
        }

        static GameObject CreateCaptureCamera(GameObject target, Canvas parentCanvas, int width, int height,
            out Canvas canvasOverride, out RenderMode originalRenderMode, out Camera originalWorldCamera, out float originalPlaneDistance)
        {
            canvasOverride = null;
            originalRenderMode = default;
            originalWorldCamera = null;
            originalPlaneDistance = 0f;

            var referenceCamera = Camera.main;
            if (!referenceCamera)
            {
#if UNITY_2023_1_OR_NEWER
                referenceCamera = UnityEngine.Object.FindFirstObjectByType<Camera>();
#else
                referenceCamera = UnityEngine.Object.FindObjectOfType<Camera>();
#endif
            }

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
                if (!parentCanvas)
                {
                    if (referenceCamera)
                    {
                        camera.transform.position = referenceCamera.transform.position;
                        camera.transform.rotation = referenceCamera.transform.rotation;
                    }
                    else
                    {
                        camera.transform.position = target.transform.position - target.transform.forward * 10f;
                        camera.transform.rotation = Quaternion.LookRotation(target.transform.forward, Vector3.up);
                    }
                    return cameraObject;
                }

                canvasOverride = parentCanvas;
                originalRenderMode = parentCanvas.renderMode;
                originalWorldCamera = parentCanvas.worldCamera;
                originalPlaneDistance = parentCanvas.planeDistance;

                if (parentCanvas.renderMode == RenderMode.WorldSpace)
                {
                    if (!referenceCamera)
                        return null;
                    PositionRectTransformCamera(camera, referenceCamera, target.GetComponent<RectTransform>(), width, height);
                    return cameraObject;
                }

                parentCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                parentCanvas.worldCamera = camera;
                parentCanvas.planeDistance = 1f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.transform.rotation = Quaternion.identity;
                return cameraObject;
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);

            var center = bounds.center;
            var direction = referenceCamera ? center - referenceCamera.transform.position : target.transform.forward;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
                direction = target.transform.forward;
            direction.Normalize();

            camera.orthographic = true;
            camera.transform.position = center - direction * 10f;
            camera.transform.rotation = Quaternion.LookRotation(direction, referenceCamera ? referenceCamera.transform.up : Vector3.up);

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

        static void PositionRectTransformCamera(Camera camera, Camera referenceCamera, RectTransform rectTransform, int width, int height)
        {
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            var bounds = new Bounds(corners[0], Vector3.zero);
            for (var index = 1; index < corners.Length; index++)
                bounds.Encapsulate(corners[index]);

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

            camera.orthographicSize = Mathf.Max(verticalExtent, horizontalExtent / ((float)width / height)) + 0.1f;
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
