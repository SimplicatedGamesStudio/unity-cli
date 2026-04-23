using NUnit.Framework;

namespace UnityCliConnector.EditorTests
{
    public class GameObjectPathTests
    {
        [Test]
        public void Parse_SceneQualifiedPath_SplitsSceneAndSegments()
        {
            var parsed = GameObjectPath.Parse("BattleScene::HUD/MainCanvas[0]");

            Assert.That(parsed.SceneName, Is.EqualTo("BattleScene"));
            Assert.That(parsed.Segments.Count, Is.EqualTo(2));
            Assert.That(parsed.Segments[1].Name, Is.EqualTo("MainCanvas"));
            Assert.That(parsed.Segments[1].Index, Is.EqualTo(0));
        }

        [Test]
        public void Parse_DuplicateSiblingWithoutIndex_IsMarkedAmbiguous()
        {
            var parsed = GameObjectPath.Parse("HUD/MainCanvas");
            Assert.That(parsed.Segments[1].HasExplicitIndex, Is.False);
        }

        [Test]
        public void Parse_PathWithMultipleSceneDelimiters_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() => GameObjectPath.Parse("SceneA::HUD::Canvas"));
        }

        [Test]
        public void Parse_PathWithEmptySceneQualifier_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() => GameObjectPath.Parse("::HUD/MainCanvas"));
        }
    }
}
