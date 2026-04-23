using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEditor;
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
                Assert.That(data["resolvedPath"]?.ToString(), Does.Contain("::" + rootName));
                Assert.That(data["source"]?.ToString(), Is.EqualTo("scene"));
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
                Assert.That(data["path"]?.ToString(), Does.Contain("::" + rootName));
                Assert.That(data["children"]?[0]?["children"], Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GetGameObjectInfo_PrefabMode_ReportsPrefabContentsStatus()
        {
            const string testFolder = "Assets/TempUnityCliConnectorTests";
            var rootName = "HUD_" + System.Guid.NewGuid().ToString("N");
            var assetPath = testFolder + "/" + rootName + ".prefab";
            var root = new GameObject(rootName);
            try
            {
                if (!AssetDatabase.IsValidFolder(testFolder))
                    AssetDatabase.CreateFolder("Assets", "TempUnityCliConnectorTests");

                PrefabUtility.SaveAsPrefabAsset(root, assetPath);

                var response = (SuccessResponse)GetGameObjectInfo.HandleCommand(new JObject
                {
                    ["prefab"] = assetPath,
                    ["path"] = rootName
                });

                var data = JObject.FromObject(response.data);
                Assert.That(data["resolvedPath"]?.ToString(), Is.EqualTo(rootName));
                Assert.That(data["source"]?.ToString(), Is.EqualTo("prefab"));
                Assert.That(data["prefabContext"]?.ToString(), Is.EqualTo("PrefabContents"));
            }
            finally
            {
                Object.DestroyImmediate(root);
                AssetDatabase.DeleteAsset(assetPath);
            }
        }
    }
}
