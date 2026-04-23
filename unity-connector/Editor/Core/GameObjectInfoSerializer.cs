using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityCliConnector
{
    public static class GameObjectInfoSerializer
    {
        public static object SerializeInfo(GameObject target, string source, bool includeSceneName)
        {
            var data = new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = target.name,
                ["resolvedPath"] = BuildResolvedPath(target, includeSceneName),
                ["source"] = source,
                ["activeSelf"] = target.activeSelf,
                ["layer"] = target.layer,
                ["tag"] = target.tag,
                ["childCount"] = target.transform.childCount,
                ["componentTypes"] = GetComponentTypes(target),
                ["transform"] = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["localPosition"] = SerializeVector3(target.transform.localPosition),
                    ["localRotation"] = SerializeVector3(target.transform.localRotation.eulerAngles),
                    ["localScale"] = SerializeVector3(target.transform.localScale),
                },
                ["prefab"] = GetPrefabStatus(target),
            };

            if (target.transform is RectTransform rectTransform)
            {
                data["rectTransform"] = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["anchoredPosition"] = SerializeVector2(rectTransform.anchoredPosition),
                    ["sizeDelta"] = SerializeVector2(rectTransform.sizeDelta),
                    ["anchorMin"] = SerializeVector2(rectTransform.anchorMin),
                    ["anchorMax"] = SerializeVector2(rectTransform.anchorMax),
                };
            }

            return data;
        }

        internal static string[] GetComponentTypes(GameObject target)
        {
            return target.GetComponents<Component>()
                .Where(component => component != null)
                .Select(component => component.GetType().Name)
                .ToArray();
        }

        internal static string BuildResolvedPath(GameObject target, bool includeSceneName)
        {
            var segments = new System.Collections.Generic.List<string>();
            for (var current = target.transform; current != null; current = current.parent)
                segments.Insert(0, BuildSegment(current));

            var path = string.Join("/", segments);
            return includeSceneName ? target.scene.name + "::" + path : path;
        }

        static string BuildSegment(Transform target)
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

        static string GetPrefabStatus(GameObject target)
        {
            if (PrefabUtility.IsPartOfPrefabContents(target))
                return "PrefabContents";

            return PrefabUtility.GetPrefabInstanceStatus(target).ToString();
        }

        private static object SerializeVector2(Vector2 value)
        {
            return new System.Collections.Generic.Dictionary<string, object>
            {
                ["x"] = value.x,
                ["y"] = value.y,
            };
        }

        private static object SerializeVector3(Vector3 value)
        {
            return new System.Collections.Generic.Dictionary<string, object>
            {
                ["x"] = value.x,
                ["y"] = value.y,
                ["z"] = value.z,
            };
        }
    }
}
