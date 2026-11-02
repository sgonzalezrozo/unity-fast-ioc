using UnityEngine;

namespace Tools.FastIoC.Runtime.API.Initialization
{
    public static class SingletonEnforcer<T> where T : MonoBehaviour
    {
        private static T instance;

        public static bool EnforceSingleton(T target)
        {
            if (target == null)
                return false;

            if (instance != null && instance != target)
            {
                MonoBehaviour.Destroy(target.gameObject);
                return false;
            }

            instance = target;
            MonoBehaviour.DontDestroyOnLoad(target.gameObject);
            return true;
        }

        public static T Instance => instance;

        public static void Reset() => instance = null;
    }
}

