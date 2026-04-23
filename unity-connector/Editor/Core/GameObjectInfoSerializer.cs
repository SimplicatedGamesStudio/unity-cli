using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityCliConnector
{
    public static class GameObjectInfoSerializer
    {
        public static object SerializeInfo(GameObject target, string resolvedPath, string source)
        {
            var data = new System.Collections.Generic.Dictionary<string, object>
            {
                ["name"] = target.name,
                ["resolvedPath"] = resolvedPath,
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
                ["prefab"] = PrefabUtility.GetPrefabInstanceStatus(target).ToString(),
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
