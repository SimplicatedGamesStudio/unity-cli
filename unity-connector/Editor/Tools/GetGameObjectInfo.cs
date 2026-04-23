using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityCliConnector.Tools
{
    [UnityCliTool(Name = "get_game_object_info", Description = "Get detailed information about a scene or prefab GameObject.")]
    public static class GetGameObjectInfo
    {
        public static object HandleCommand(JObject parameters)
        {
            GameObject prefabRoot = null;

            try
            {
                var p = new ToolParams(parameters ?? new JObject());
                var pathResult = p.GetRequired("path");
                if (!pathResult.IsSuccess)
                    return new ErrorResponse(pathResult.ErrorMessage);

                var prefab = p.Get("prefab");
                var target = ResolveTarget(prefab, pathResult.Value, ref prefabRoot);
                var source = string.IsNullOrEmpty(prefab) ? "scene" : "prefab";
                var data = GameObjectInfoSerializer.SerializeInfo(target, pathResult.Value, source);

                return new SuccessResponse("GameObject info", data);
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

        private static GameObject ResolveTarget(string prefab, string path, ref GameObject prefabRoot)
        {
            if (string.IsNullOrEmpty(prefab))
                return GameObjectResolver.ResolveSceneObject(path);

            prefabRoot = GameObjectResolver.LoadPrefabRoot(prefab);
            return GameObjectResolver.ResolvePrefabObject(prefabRoot, path);
        }
    }
}
