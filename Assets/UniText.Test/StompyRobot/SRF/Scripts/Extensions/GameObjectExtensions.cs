namespace SRF
{
    using UnityEngine;

    public static class SRFGameObjectExtensions
    {
        public static T GetIComponent<T>(this GameObject t) where T : class
        {
            return t.GetComponent(typeof (T)) as T;
        }

                /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T GetComponentOrAdd<T>(this GameObject obj) where T : Component
        {
            var t = obj.GetComponent<T>();

            if (t == null)
            {
                t = obj.AddComponent<T>();
            }

            return t;
        }

                /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        public static void RemoveComponentIfExists<T>(this GameObject obj) where T : Component
        {
            var t = obj.GetComponent<T>();

            if (t != null)
            {
                Object.Destroy(t);
            }
        }

                /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        public static void RemoveComponentsIfExists<T>(this GameObject obj) where T : Component
        {
            var t = obj.GetComponents<T>();

            for (var i = 0; i < t.Length; i++)
            {
                Object.Destroy(t[i]);
            }
        }

                /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="enable"></param>
        /// <returns>True if the component exists</returns>
        public static bool EnableComponentIfExists<T>(this GameObject obj, bool enable = true) where T : MonoBehaviour
        {
            var t = obj.GetComponent<T>();

            if (t == null)
            {
                return false;
            }

            t.enabled = enable;

            return true;
        }

                /// <param name="o"></param>
        /// <param name="layer"></param>
        public static void SetLayerRecursive(this GameObject o, int layer)
        {
            SetLayerInternal(o.transform, layer);
        }

        private static void SetLayerInternal(Transform t, int layer)
        {
            t.gameObject.layer = layer;

            foreach (Transform o in t)
            {
                SetLayerInternal(o, layer);
            }
        }
    }
}
