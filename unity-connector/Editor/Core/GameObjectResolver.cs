using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityCliConnector
{
    public static class GameObjectResolver
    {
        public static GameObject ResolveSceneObject(string rawPath)
        {
            var path = GameObjectPath.Parse(rawPath);
            if (SceneManager.sceneCount > 1 && string.IsNullOrEmpty(path.SceneName))
                throw new InvalidOperationException("scene-qualified path required when multiple scenes are loaded");

            var scene = string.IsNullOrEmpty(path.SceneName)
                ? SceneManager.GetActiveScene()
                : SceneManager.GetSceneByName(path.SceneName);

            if (!scene.IsValid())
                throw new InvalidOperationException($"scene not found: {path.SceneName}");

            return ResolveParsedPath(path, scene.GetRootGameObjects());
        }

        public static GameObject LoadPrefabRoot(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (!root)
                throw new InvalidOperationException($"prefab not found: {prefabPath}");
            return root;
        }

        public static GameObject ResolvePrefabObject(GameObject prefabRoot, string rawPath)
        {
            var path = GameObjectPath.Parse(rawPath);
            return ResolveParsedPath(path, new[] { prefabRoot });
        }

        internal static GameObject ResolveParsedPath(GameObjectPath path, IEnumerable<GameObject> startingNodes)
        {
            IEnumerable<GameObject> current = startingNodes;
            GameObject resolved = null;

            foreach (var segment in path.Segments)
            {
                var matches = current.Where(go => go.name == segment.Name).ToList();
                if (matches.Count == 0)
                    throw new InvalidOperationException($"object not found: {segment.Name}");

                if (segment.Index.HasValue)
                {
                    if (segment.Index.Value < 0 || segment.Index.Value >= matches.Count)
                        throw new InvalidOperationException($"object index out of range: {segment.Name}[{segment.Index.Value}]");

                    resolved = matches[segment.Index.Value];
                }
                else
                {
                    if (matches.Count > 1)
                        throw new InvalidOperationException($"ambiguous path segment: {segment.Name}");

                    resolved = matches[0];
                }

                current = resolved.transform.Cast<Transform>().Select(t => t.gameObject);
            }

            return resolved;
        }
    }
}
