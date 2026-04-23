using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityCliConnector
{
    public static class UiCaptureUtility
    {
        public static Camera ConfigureCaptureCamera(Canvas sourceCanvas, Canvas cloneCanvas, GameObject cameraObject, int width, int height)
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
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

        public static void DisableUnrelatedSceneContent(GameObject cloneRoot, List<Canvas> disabledCanvases, List<Renderer> disabledRenderers)
        {
            var cloneTransforms = new HashSet<Transform>(cloneRoot.GetComponentsInChildren<Transform>(true));

            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.enabled)
                    continue;
                if (cloneTransforms.Contains(canvas.transform))
                    continue;

                canvas.enabled = false;
                disabledCanvases.Add(canvas);
            }

            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!renderer.enabled)
                    continue;
                if (cloneTransforms.Contains(renderer.transform))
                    continue;

                renderer.enabled = false;
                disabledRenderers.Add(renderer);
            }
        }

        public static void PositionWorldSpaceCamera(Camera camera, Camera referenceCamera, RectTransform rectTransform, int width, int height)
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
