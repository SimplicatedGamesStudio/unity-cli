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
