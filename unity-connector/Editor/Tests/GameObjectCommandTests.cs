using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityCliConnector.Tools;

namespace UnityCliConnector.EditorTests
{
    public class GameObjectCommandTests
    {
        [Test]
        public void GetGameObjectInfo_ReturnsCoreSchema()
        {
            var rootName = "HUD_" + System.Guid.NewGuid().ToString("N");
            var root = new GameObject(rootName);
            try
            {
                var canvas = root.AddComponent<Canvas>();
                var response = (SuccessResponse)GetGameObjectInfo.HandleCommand(new JObject
                {
                    ["path"] = rootName
                });

                var data = JObject.FromObject(response.data);
                Assert.That(data["name"]?.ToString(), Is.EqualTo(rootName));
                Assert.That(data["componentTypes"]?.ToObject<string[]>(), Does.Contain(nameof(Canvas)));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ListGameObjectsInHierarchy_DefaultsToDepthOne()
        {
            var rootName = "HUD_" + System.Guid.NewGuid().ToString("N");
            var root = new GameObject(rootName);
            try
            {
                var child = new GameObject("Panel");
                var grandChild = new GameObject("Label");
                child.transform.SetParent(root.transform);
                grandChild.transform.SetParent(child.transform);

                var response = (SuccessResponse)ListGameObjectsInHierarchy.HandleCommand(new JObject
                {
                    ["path"] = rootName
                });

                var data = JObject.FromObject(response.data);
                Assert.That(data["children"]?[0]?["children"], Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
