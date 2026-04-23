using NUnit.Framework;
using UnityEngine;

namespace UnityCliConnector.EditorTests
{
    public class GameObjectResolverTests
    {
        [Test]
        public void ResolveSceneObject_DuplicateSiblingWithoutIndex_Throws()
        {
            var root = new GameObject("HUD");
            var first = new GameObject("Panel");
            var second = new GameObject("Panel");
            first.transform.SetParent(root.transform);
            second.transform.SetParent(root.transform);

            Assert.Throws<System.InvalidOperationException>(() =>
                GameObjectResolver.ResolveSceneObject("HUD/Panel"));

            Object.DestroyImmediate(root);
        }

        [Test]
        public void ResolveSceneObject_ExplicitIndex_ReturnsExpectedChild()
        {
            var root = new GameObject("HUD");
            var first = new GameObject("Panel");
            var second = new GameObject("Panel");
            first.transform.SetParent(root.transform);
            second.transform.SetParent(root.transform);

            var resolved = GameObjectResolver.ResolveSceneObject("HUD/Panel[1]");

            Assert.That(resolved, Is.EqualTo(second));
            Object.DestroyImmediate(root);
        }
    }
}
