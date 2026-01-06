namespace SRF
{
    using System.Collections.Generic;
    using UnityEngine;

    public static class SRFTransformExtensions
    {
        public static IEnumerable<Transform> GetChildren(this Transform t)
        {
            var i = 0;

            while (i < t.childCount)
            {
                yield return t.GetChild(i);
                ++i;
            }
        }

                /// <param name="t"></param>
        public static void ResetLocal(this Transform t)
        {
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

                /// <param name="t"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static GameObject CreateChild(this Transform t, string name)
        {
            var go = new GameObject(name);
            go.transform.parent = t;
            go.transform.ResetLocal();
            go.gameObject.layer = t.gameObject.layer;

            return go;
        }

                /// <param name="t"></param>
        /// <param name="parent"></param>
        public static void SetParentMaintainLocals(this Transform t, Transform parent)
        {
            t.SetParent(parent, false);
        }

                /// <param name="t"></param>
        /// <param name="from"></param>
        public static void SetLocals(this Transform t, Transform from)
        {
            t.localPosition = from.localPosition;
            t.localRotation = from.localRotation;
            t.localScale = from.localScale;
        }

                /// <param name="t"></param>
        /// <param name="from"></param>
        public static void Match(this Transform t, Transform from)
        {
            t.position = from.position;
            t.rotation = from.rotation;
        }

                /// <param name="t"></param>
        public static void DestroyChildren(this Transform t)
        {
            foreach (var child in t)
            {
                Object.Destroy(((Transform) child).gameObject);
            }
        }
    }
}
