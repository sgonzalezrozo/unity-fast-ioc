#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using FastIoC.Core;

namespace FastIoC.Editor.Generation
{
    public static class TypeScopeAnalyzer
    {
        public static Dictionary<Type, HashSet<string>> BuildTypeScopeMap(
            Dictionary<string, List<Type>> installersByScene,
            Dictionary<Type, List<Type>> installerBindings,
            List<Type> allInstallers,
            HashSet<Type> reachableTypes)
        {
            Dictionary<Type, HashSet<string>> typeToScopes = new();
            Dictionary<Type, HashSet<string>> installerToScopes = MapInstallersToScopes(installersByScene, allInstallers);

            foreach (KeyValuePair<Type, List<Type>> kvp in installerBindings)
            {
                Type installer = kvp.Key;
                List<Type> boundTypes = kvp.Value;

                if (!installerToScopes.ContainsKey(installer))
                    continue;

                HashSet<string> installerScopes = installerToScopes[installer];

                foreach (Type boundType in boundTypes)
                {
                    if (!typeToScopes.ContainsKey(boundType))
                        typeToScopes[boundType] = new HashSet<string>();

                    typeToScopes[boundType].UnionWith(installerScopes);
                }
            }

            foreach (Type type in reachableTypes)
            {
                if (!typeToScopes.ContainsKey(type))
                    typeToScopes[type] = new HashSet<string> { FastIoCConstants.GLOBAL_SCOPE };
            }

            return typeToScopes;
        }

        private static Dictionary<Type, HashSet<string>> MapInstallersToScopes(
            Dictionary<string, List<Type>> installersByScene,
            List<Type> allInstallers)
        {
            Dictionary<Type, HashSet<string>> installerToScopes = new();

            foreach (Type installer in allInstallers)
            {
                HashSet<string> scopes = new();

                if (installersByScene.ContainsKey(FastIoCConstants.GLOBAL_SCOPE) && installersByScene[FastIoCConstants.GLOBAL_SCOPE].Contains(installer))
                    scopes.Add(FastIoCConstants.GLOBAL_SCOPE);

                foreach (KeyValuePair<string, List<Type>> kvp in installersByScene.Where(kv => kv.Key != FastIoCConstants.GLOBAL_SCOPE))
                {
                    if (kvp.Value.Contains(installer))
                        scopes.Add(kvp.Key);
                }

                installerToScopes[installer] = scopes;
            }

            return installerToScopes;
        }
    }
}
#endif

