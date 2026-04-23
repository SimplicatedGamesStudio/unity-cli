using System;
using System.Linq;
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
                        return new SuccessResponse("GameObject hierarchy", SerializeNode(prefabRoot, depth, false));

                    var target = GameObjectResolver.ResolvePrefabObject(prefabRoot, path);
                    return new SuccessResponse("GameObject hierarchy", SerializeNode(target, depth, false));
                }

                if (string.IsNullOrEmpty(path))
                {
                    var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                    return new SuccessResponse("GameObject hierarchy", roots
                        .Select(root => SerializeNode(root, depth, true))
                        .ToArray());
                }

                var sceneTarget = GameObjectResolver.ResolveSceneObject(path);
                return new SuccessResponse("GameObject hierarchy", SerializeNode(sceneTarget, depth, true));
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

        static object SerializeNode(GameObject target, int depth, bool includeSceneName)
        {
            var data = new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = target.name,
                ["path"] = GameObjectInfoSerializer.BuildResolvedPath(target, includeSceneName),
                ["activeSelf"] = target.activeSelf,
                ["childCount"] = target.transform.childCount,
                ["componentTypes"] = GameObjectInfoSerializer.GetComponentTypes(target),
            };

            if (depth != 0)
            {
                var nextDepth = depth < 0 ? -1 : depth - 1;
                data["children"] = target.transform.Cast<Transform>()
                    .Select(child => SerializeNode(child.gameObject, nextDepth, includeSceneName))
                    .ToArray();
            }

            return data;
        }
    }
}
