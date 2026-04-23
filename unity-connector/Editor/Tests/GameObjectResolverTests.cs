using NUnit.Framework;
using UnityEngine;

namespace UnityCliConnector.EditorTests
{
    public class GameObjectResolverTests
    {
        [Test]
        public void ResolveSceneObject_DuplicateSiblingWithoutIndex_Throws()
        {
            var rootName = "HUD_" + System.Guid.NewGuid().ToString("N");
            var root = new GameObject(rootName);
            var first = new GameObject("Panel");
            var second = new GameObject("Panel");
            first.transform.SetParent(root.transform);
            second.transform.SetParent(root.transform);

            Assert.Throws<System.InvalidOperationException>(() =>
                GameObjectResolver.ResolveSceneObject(rootName + "/Panel"));

            Object.DestroyImmediate(root);
        }

        [Test]
        public void ResolveSceneObject_ExplicitIndex_ReturnsExpectedChild()
        {
            var rootName = "HUD_" + System.Guid.NewGuid().ToString("N");
            var root = new GameObject(rootName);
            var first = new GameObject("Panel");
            var second = new GameObject("Panel");
            first.transform.SetParent(root.transform);
            second.transform.SetParent(root.transform);

            var resolved = GameObjectResolver.ResolveSceneObject(rootName + "/Panel[1]");

            Assert.That(resolved, Is.EqualTo(second));
            Object.DestroyImmediate(root);
        }

        [Test]
        public void ResolvePrefabObject_SceneQualifiedPath_Throws()
        {
            var rootName = "Prefab_" + System.Guid.NewGuid().ToString("N");
            var root = new GameObject(rootName);

            Assert.Throws<System.InvalidOperationException>(() =>
                GameObjectResolver.ResolvePrefabObject(root, "SceneA::" + rootName));

            Object.DestroyImmediate(root);
        }
    }
}
