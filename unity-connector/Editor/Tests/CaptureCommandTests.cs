using System.IO;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
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
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void CaptureUiCanvas_WritesFile()
        {
            var rootName = "HUD_" + System.Guid.NewGuid().ToString("N");
            var outputPath = "Screenshots/test-ui-capture.png";
            var target = new GameObject(rootName);
            var cameraObject = new GameObject("MainCamera");
            string capturedPath = null;
            try
            {
                target.AddComponent<Canvas>();
                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<Camera>();

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
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
                if (!string.IsNullOrEmpty(capturedPath) && File.Exists(capturedPath))
                    File.Delete(capturedPath);
            }
        }
    }
}
