using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityCliConnector.Tools
{
    [UnityCliTool(Name = "list_game_objects_in_hierarchy", Description = "List GameObjects in the active scene or a prefab hierarchy.")]
    public static class ListGameObjectsInHierarchy
    {
        public static object HandleCommand(JObject parameters)
        {
            GameObject prefabRoot = null;

            try
            {
                var p = new ToolParams(parameters ?? new JObject());
                var prefab = p.Get("prefab");
                var path = p.Get("path");
                var recursive = p.GetBool("recursive");
                var depth = recursive ? -1 : p.GetInt("depth", 1).Value;

                if (!string.IsNullOrEmpty(prefab))
                {
                    prefabRoot = GameObjectResolver.LoadPrefabRoot(prefab);
                    if (string.IsNullOrEmpty(path))
                        return new SuccessResponse("GameObject hierarchy", SerializeNode(prefabRoot, BuildExactPath(prefabRoot), depth));

                    var target = GameObjectResolver.ResolvePrefabObject(prefabRoot, path);
                    return new SuccessResponse("GameObject hierarchy", SerializeNode(target, path, depth));
                }

                if (string.IsNullOrEmpty(path))
                {
                    var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                    return new SuccessResponse("GameObject hierarchy", roots
                        .Select(root => SerializeNode(root, BuildExactPath(root), depth))
                        .ToArray());
                }

                var sceneTarget = GameObjectResolver.ResolveSceneObject(path);
                return new SuccessResponse("GameObject hierarchy", SerializeNode(sceneTarget, path, depth));
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message);
            }
            finally
            {
                if (prefabRoot != null)
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        static object SerializeNode(GameObject target, string path, int depth)
        {
            var data = new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = target.name,
                ["path"] = path,
                ["activeSelf"] = target.activeSelf,
                ["childCount"] = target.transform.childCount,
                ["componentTypes"] = GameObjectInfoSerializer.GetComponentTypes(target),
            };

            if (depth != 0)
            {
                var nextDepth = depth < 0 ? -1 : depth - 1;
                data["children"] = target.transform.Cast<Transform>()
                    .Select(child => SerializeNode(child.gameObject, path + "/" + BuildExactSegment(child), nextDepth))
                    .ToArray();
            }

            return data;
        }

        static string BuildExactPath(GameObject target)
        {
            var segments = new System.Collections.Generic.List<string>();
            for (var current = target.transform; current != null; current = current.parent)
                segments.Insert(0, BuildExactSegment(current));

            return string.Join("/", segments);
        }

        static string BuildExactSegment(Transform target)
        {
            var siblings = GetSiblings(target).Where(candidate => candidate.name == target.name).ToList();
            if (siblings.Count <= 1)
                return target.name;

            return target.name + "[" + siblings.IndexOf(target) + "]";
        }

        static System.Collections.Generic.IEnumerable<Transform> GetSiblings(Transform target)
        {
            if (target.parent != null)
                return target.parent.Cast<Transform>();

            return target.gameObject.scene.GetRootGameObjects().Select(root => root.transform);
        }
    }
}
