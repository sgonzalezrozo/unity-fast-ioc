using System;
using System.Linq;

namespace FastIoC.API
{
    /// <summary>Marks a BindingRecorder as an installer, so it is discovered automatically.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class MonoInstallerAttribute : Attribute
    {
        /// <summary>The scene names this installer applies to. "*" means global.</summary>
        public string[] Scenes { get; private set; }
        /// <summary>Installers with a higher priority run first. The default is 0.</summary>
        public int Priority { get; set; }
        /// <summary>Excludes this installer from code generation.</summary>
        public bool Disabled { get; set; }

        /// <summary>Creates a global installer.</summary>
        public MonoInstallerAttribute()
        {
            Scenes = new[] { "*" };
        }

        /// <summary>Creates an installer for the given scene names.</summary>
        public MonoInstallerAttribute(params string[] scenes)
        {
            if (scenes == null || scenes.Length == 0)
            {
                Scenes = new[] { "*" };  // Default to Global if empty
                return;
            }

            Scenes = scenes;
        }

        /// <summary>Checks whether this installer applies to a scene.</summary>
        public bool AppliesToScene(string sceneName)
        {
            if (Scenes == null || Scenes.Length == 0)
                return true;

            // "*" means Global - applies to all scenes
            if (Enumerable.Contains(Scenes, "*"))
                return true;

            return Enumerable.Contains(Scenes, sceneName);
        }

        /// <summary>True when this installer applies to every scene.</summary>
        public bool IsGlobal => Scenes != null && (Scenes.Length == 0 || Enumerable.Contains(Scenes, "*"));
    }
}
