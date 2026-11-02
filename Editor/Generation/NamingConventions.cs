#if UNITY_EDITOR
namespace Tools.FastIoC.Editor.Generation
{
    public static class NamingConventions
    {
        public static string EscapeSceneName(string sceneName)
        {
            return sceneName
                .Replace(" ", "_")
                .Replace("-", "_")
                .Replace("/", "_");
        }

        public static string GetActivationMethodName(string fullTypeName)
        {
            return fullTypeName
                .Replace(".", "_")
                .Replace("+", "_");
        }
    }
}
#endif

