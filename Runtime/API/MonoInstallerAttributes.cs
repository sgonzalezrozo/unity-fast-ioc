using System;
using System.Linq;

namespace FastIoC.API
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class MonoInstallerAttribute : Attribute
    {
        public string[] Scenes { get; private set; }
        public int Priority { get; set; }
        public bool Disabled { get; set; }

        public MonoInstallerAttribute()
        {
            Scenes = new[] { "*" };
        }

        public MonoInstallerAttribute(params string[] scenes)
        {
            if (scenes == null || scenes.Length == 0)
            {
                Scenes = new[] { "*" };  // Default to Global if empty
                return;
            }

            Scenes = scenes;
        }

        public bool AppliesToScene(string sceneName)
        {
            if (Scenes == null || Scenes.Length == 0)
                return true;

            // "*" means Global - applies to all scenes
            if (Enumerable.Contains(Scenes, "*"))
                return true;

            return Enumerable.Contains(Scenes, sceneName);
        }

        public bool IsGlobal => Scenes != null && (Scenes.Length == 0 || Enumerable.Contains(Scenes, "*"));
    }
}
