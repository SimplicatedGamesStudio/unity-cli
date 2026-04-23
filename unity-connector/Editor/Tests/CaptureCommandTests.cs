using System.IO;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine.UI;
using UnityEngine;
using UnityCliConnector.Tools;

namespace UnityCliConnector.EditorTests
{
    public class CaptureCommandTests
    {
        [Test]
        public void CaptureUiCanvas_RejectsNonCanvasTarget()
        {
            var rootName = "HUD_" + System.Guid.NewGuid().ToString("N");
            var target = new GameObject(rootName);
            try
            {
                var result = CaptureUiCanvas.HandleCommand(new JObject
                {
                    ["path"] = rootName
                });

                Assert.That(result, Is.TypeOf<ErrorResponse>());
                Assert.That(((ErrorResponse)result).message, Is.EqualTo("invalid_target_type"));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void CaptureUiCanvas_NoCameraForWorldSpaceCanvas_ReturnsCaptureFailed()
        {
            var rootName = "HUD_" + System.Guid.NewGuid().ToString("N");
            var target = CreateCanvasRoot(rootName, RenderMode.WorldSpace);
            var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            var originalStates = new bool[cameras.Length];
            for (var index = 0; index < cameras.Length; index++)
            {
                originalStates[index] = cameras[index].enabled;
                cameras[index].enabled = false;
            }

            try
            {
                var result = CaptureUiCanvas.HandleCommand(new JObject
                {
                    ["path"] = rootName
                });

                Assert.That(result, Is.TypeOf<ErrorResponse>());
                Assert.That(((ErrorResponse)result).message, Is.EqualTo("capture_failed"));
            }
            finally
            {
                for (var index = 0; index < cameras.Length; index++)
                    if (cameras[index])
                        cameras[index].enabled = originalStates[index];

                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void CaptureUiCanvas_CapturesOnlyTargetCanvas()
        {
            var rootName = "HUD_" + System.Guid.NewGuid().ToString("N");
            var otherRootName = "Other_" + System.Guid.NewGuid().ToString("N");
            var outputPath = "Screenshots/test-ui-capture.png";
            var target = CreateCanvasRoot(rootName, RenderMode.ScreenSpaceOverlay);
            var other = CreateCanvasRoot(otherRootName, RenderMode.ScreenSpaceOverlay);
            string capturedPath = null;
            Texture2D capturedTexture = null;
            try
            {
                AddFullscreenImage(target.transform, Color.red);

                var otherCanvas = other.GetComponent<Canvas>();
                otherCanvas.sortingOrder = 10;
                AddFullscreenImage(other.transform, Color.green);

                var result = (SuccessResponse)CaptureUiCanvas.HandleCommand(new JObject
                {
                    ["path"] = rootName,
                    ["output_path"] = outputPath,
                    ["width"] = 512,
                    ["height"] = 512,
                });

                var data = JObject.FromObject(result.data);
                capturedPath = data["path"]?.ToString();
                Assert.That(File.Exists(capturedPath), Is.True);

                capturedTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                capturedTexture.LoadImage(File.ReadAllBytes(capturedPath));
                var pixel = capturedTexture.GetPixel(capturedTexture.width / 2, capturedTexture.height / 2);
                Assert.That(pixel.r, Is.GreaterThan(0.5f));
                Assert.That(pixel.g, Is.LessThan(0.25f));
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(other);
                if (capturedTexture)
                    Object.DestroyImmediate(capturedTexture);
                if (!string.IsNullOrEmpty(capturedPath) && File.Exists(capturedPath))
                    File.Delete(capturedPath);
            }
        }

        [Test]
        public void CaptureUiCanvas_WorldSpaceCanvas_UsesSourceViewDirection()
        {
            var rootName = "WorldHUD_" + System.Guid.NewGuid().ToString("N");
            var outputPath = "Screenshots/test-world-ui-capture.png";
            var target = CreateCanvasRoot(rootName, RenderMode.WorldSpace);
            var cameraObject = new GameObject("MainCamera");
            Texture2D capturedTexture = null;
            string capturedPath = null;
            try
            {
                target.transform.position = Vector3.zero;
                target.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
                target.GetComponent<RectTransform>().sizeDelta = new Vector2(4f, 4f);
                AddFullscreenImage(target.transform, Color.red);

                cameraObject.tag = "MainCamera";
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.transform.LookAt(target.transform.position);

                var canvas = target.GetComponent<Canvas>();
                canvas.worldCamera = camera;

                var result = (SuccessResponse)CaptureUiCanvas.HandleCommand(new JObject
                {
                    ["path"] = rootName,
                    ["output_path"] = outputPath,
                    ["width"] = 512,
                    ["height"] = 512,
                });

                var data = JObject.FromObject(result.data);
                capturedPath = data["path"]?.ToString();
                Assert.That(File.Exists(capturedPath), Is.True);

                capturedTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                capturedTexture.LoadImage(File.ReadAllBytes(capturedPath));
                var pixel = capturedTexture.GetPixel(capturedTexture.width / 2, capturedTexture.height / 2);
                Assert.That(pixel.r, Is.GreaterThan(0.5f));
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
                if (capturedTexture)
                    Object.DestroyImmediate(capturedTexture);
                if (!string.IsNullOrEmpty(capturedPath) && File.Exists(capturedPath))
                    File.Delete(capturedPath);
            }
        }

        [Test]
        public void CaptureSceneObject_RestoresHiddenSiblings()
        {
            var rootName = "SceneRoot_" + System.Guid.NewGuid().ToString("N");
            var outputPath = "Screenshots/test-scene-capture.png";
            var root = new GameObject(rootName);
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var sibling = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var cameraObject = new GameObject("MainCamera");
            string capturedPath = null;

            try
            {
                target.name = "Target";
                sibling.name = "Sibling";
                target.transform.SetParent(root.transform);
                sibling.transform.SetParent(root.transform);
                target.transform.position = new Vector3(-1f, 0f, 0f);
                sibling.transform.position = new Vector3(1f, 0f, 0f);

                cameraObject.tag = "MainCamera";
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.transform.LookAt(target.transform.position);

                var result = (SuccessResponse)CaptureSceneObject.HandleCommand(new JObject
                {
                    ["path"] = rootName + "/Target[0]",
                    ["output_path"] = outputPath,
                    ["width"] = 512,
                    ["height"] = 512,
                });

                var data = JObject.FromObject(result.data);
                capturedPath = data["path"]?.ToString();
                Assert.That(File.Exists(capturedPath), Is.True);
                Assert.That(sibling.GetComponent<Renderer>().enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraObject);
                if (!string.IsNullOrEmpty(capturedPath) && File.Exists(capturedPath))
                    File.Delete(capturedPath);
            }
        }

        [Test]
        public void CaptureSceneObject_CapturesUiOnlyTargetSubtree()
        {
            var rootName = "CanvasRoot_" + System.Guid.NewGuid().ToString("N");
            var outputPath = "Screenshots/test-scene-capture-ui-subtree.png";
            var canvasRoot = CreateCanvasRoot(rootName, RenderMode.ScreenSpaceOverlay);
            var target = new GameObject("Target", typeof(RectTransform));
            var targetImage = new GameObject("Chip", typeof(RectTransform), typeof(Image));
            Texture2D capturedTexture = null;
            string capturedPath = null;

            try
            {
                AddFullscreenImage(canvasRoot.transform, Color.green);
                target.transform.SetParent(canvasRoot.transform, false);
                targetImage.transform.SetParent(target.transform, false);

                var rectTransform = targetImage.GetComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.sizeDelta = new Vector2(128f, 128f);
                rectTransform.anchoredPosition = Vector2.zero;
                targetImage.GetComponent<Image>().color = Color.red;

                var result = (SuccessResponse)CaptureSceneObject.HandleCommand(new JObject
                {
                    ["path"] = rootName + "/Target",
                    ["output_path"] = outputPath,
                    ["width"] = 512,
                    ["height"] = 512,
                });

                var data = JObject.FromObject(result.data);
                capturedPath = data["path"]?.ToString();
                Assert.That(File.Exists(capturedPath), Is.True);

                capturedTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                capturedTexture.LoadImage(File.ReadAllBytes(capturedPath));
                var pixel = capturedTexture.GetPixel(capturedTexture.width / 2, capturedTexture.height / 2);
                Assert.That(pixel.r, Is.GreaterThan(0.5f));
                Assert.That(pixel.g, Is.LessThan(0.25f));
                Assert.That(canvasRoot.GetComponent<Canvas>().enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(canvasRoot);
                if (capturedTexture)
                    Object.DestroyImmediate(capturedTexture);
                if (!string.IsNullOrEmpty(capturedPath) && File.Exists(capturedPath))
                    File.Delete(capturedPath);
            }
        }

        [Test]
        public void CaptureSceneObject_CapturesWorldSpaceUiSubtree()
        {
            var rootName = "WorldCanvasRoot_" + System.Guid.NewGuid().ToString("N");
            var outputPath = "Screenshots/test-scene-capture-worldspace-ui-subtree.png";
            var canvasRoot = CreateCanvasRoot(rootName, RenderMode.WorldSpace);
            var target = new GameObject("Target", typeof(RectTransform));
            var targetImage = new GameObject("Chip", typeof(RectTransform), typeof(Image));
            var cameraObject = new GameObject("MainCamera");
            Texture2D capturedTexture = null;
            string capturedPath = null;

            try
            {
                canvasRoot.transform.position = Vector3.zero;
                canvasRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(4f, 4f);
                AddFullscreenImage(canvasRoot.transform, Color.green);

                target.transform.SetParent(canvasRoot.transform, false);
                targetImage.transform.SetParent(target.transform, false);

                var rectTransform = targetImage.GetComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.sizeDelta = new Vector2(1f, 1f);
                rectTransform.anchoredPosition = new Vector2(-1.25f, 0.5f);
                targetImage.GetComponent<Image>().color = Color.red;

                cameraObject.tag = "MainCamera";
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.transform.LookAt(canvasRoot.transform.position);
                canvasRoot.GetComponent<Canvas>().worldCamera = camera;

                var result = (SuccessResponse)CaptureSceneObject.HandleCommand(new JObject
                {
                    ["path"] = rootName + "/Target",
                    ["output_path"] = outputPath,
                    ["width"] = 512,
                    ["height"] = 512,
                });

                var data = JObject.FromObject(result.data);
                capturedPath = data["path"]?.ToString();
                Assert.That(File.Exists(capturedPath), Is.True);

                capturedTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                capturedTexture.LoadImage(File.ReadAllBytes(capturedPath));
                var pixel = capturedTexture.GetPixel(capturedTexture.width / 2, capturedTexture.height / 2);
                Assert.That(pixel.r, Is.GreaterThan(0.5f));
                Assert.That(pixel.g, Is.LessThan(0.25f));
            }
            finally
            {
                Object.DestroyImmediate(canvasRoot);
                Object.DestroyImmediate(cameraObject);
                if (capturedTexture)
                    Object.DestroyImmediate(capturedTexture);
                if (!string.IsNullOrEmpty(capturedPath) && File.Exists(capturedPath))
                    File.Delete(capturedPath);
            }
        }

        [Test]
        public void CaptureSceneObject_UsesFramingCameraEvenWhenMainCameraFacesAway()
        {
            var rootName = "SceneRoot_" + System.Guid.NewGuid().ToString("N");
            var outputPath = "Screenshots/test-scene-capture-framed.png";
            var root = new GameObject(rootName);
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cameraObject = new GameObject("MainCamera");
            Texture2D capturedTexture = null;
            string capturedPath = null;

            try
            {
                target.name = "Target";
                target.transform.SetParent(root.transform);
                target.transform.position = Vector3.zero;
                target.GetComponent<Renderer>().sharedMaterial.color = Color.red;

                cameraObject.tag = "MainCamera";
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                var result = (SuccessResponse)CaptureSceneObject.HandleCommand(new JObject
                {
                    ["path"] = rootName + "/Target",
                    ["output_path"] = outputPath,
                    ["width"] = 512,
                    ["height"] = 512,
                });

                var data = JObject.FromObject(result.data);
                capturedPath = data["path"]?.ToString();
                Assert.That(File.Exists(capturedPath), Is.True);

                capturedTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                capturedTexture.LoadImage(File.ReadAllBytes(capturedPath));
                var pixel = capturedTexture.GetPixel(capturedTexture.width / 2, capturedTexture.height / 2);
                Assert.That(pixel.r, Is.GreaterThan(0.25f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraObject);
                if (capturedTexture)
                    Object.DestroyImmediate(capturedTexture);
                if (!string.IsNullOrEmpty(capturedPath) && File.Exists(capturedPath))
                    File.Delete(capturedPath);
            }
        }

        static GameObject CreateCanvasRoot(string name, RenderMode renderMode)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            var rectTransform = root.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            root.GetComponent<Canvas>().renderMode = renderMode;
            return root;
        }

        static void AddFullscreenImage(Transform parent, Color color)
        {
            var child = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            child.transform.SetParent(parent, false);
            var rectTransform = child.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            child.GetComponent<Image>().color = color;
        }
    }
}
