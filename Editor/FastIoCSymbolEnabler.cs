#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace FastIoC.Editor
{
    [InitializeOnLoad]
    public static class FastIoCSymbolEnabler
    {
        private const string FAST_IOC_SYMBOL = "FAST_IoC";

        static FastIoCSymbolEnabler()
        {
            EditorApplication.delayCall += EnsureSymbolEnabledForAllPlatforms;
        }

        public static void EnsureFastIoCSymbolEnabled()
        {
            EnsureSymbolEnabledForAllPlatforms();
        }

        private static void EnsureSymbolEnabledForAllPlatforms()
        {
            bool anyChanges = false;
            NamedBuildTarget[] namedTargets = GetAllNamedBuildTargets();

            foreach (NamedBuildTarget namedTarget in namedTargets)
            {
                try
                {
                    PlayerSettings.GetScriptingDefineSymbols(namedTarget, out string[] defines);
                    string definesString = string.Join(";", defines);

                    if (ContainsSymbol(definesString, FAST_IOC_SYMBOL)) 
                        continue;
                    
                    AddSymbolToTarget(namedTarget, defines);
                    anyChanges = true;
                }
                catch (Exception)
                {
                    // Skip platforms that aren't available/installed
                }
            }

            if (anyChanges)
            {
                Debug.Log("[FastIoC] FAST_IoC symbol enabled for all build targets. Scripts will recompile.");
            }
        }

        private static bool ContainsSymbol(string defines, string symbol)
        {
            if (string.IsNullOrEmpty(defines))
                return false;

            string[] symbols = defines.Split(';');
            for (int i = 0; i < symbols.Length; i++)
            {
                if (symbols[i].Trim() == symbol)
                    return true;
            }
            return false;
        }

        private static NamedBuildTarget[] GetAllNamedBuildTargets()
        {
            return new[]
            {
                NamedBuildTarget.Standalone,
                NamedBuildTarget.iOS,
                NamedBuildTarget.Android,
                NamedBuildTarget.WebGL,
                NamedBuildTarget.WindowsStoreApps,
                NamedBuildTarget.PS4,
                NamedBuildTarget.XboxOne,
                NamedBuildTarget.tvOS,
                NamedBuildTarget.NintendoSwitch
            };
        }

        private static void AddSymbolToTarget(NamedBuildTarget namedTarget, string[] currentDefines)
        {
            bool hasSymbol = false;
            for (int i = 0; i < currentDefines.Length; i++)
            {
                if (currentDefines[i].Trim() != FAST_IOC_SYMBOL) 
                    continue;
                
                hasSymbol = true;
                break;
            }

            if (hasSymbol)
                return;

            string[] newDefines = new string[currentDefines.Length + 1];
            Array.Copy(currentDefines, newDefines, currentDefines.Length);
            newDefines[currentDefines.Length] = FAST_IOC_SYMBOL;

            PlayerSettings.SetScriptingDefineSymbols(namedTarget, newDefines);
            Debug.Log($"[FastIoC] Enabled FAST_IoC symbol for {namedTarget}");
        }
    }
}
#endif
