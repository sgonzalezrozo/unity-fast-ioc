using System;
using System.Collections.Generic;
using System.Linq;

namespace Tools.FastIoC.Runtime.Core
{
    public class SceneContainerStack
    {
        private struct SceneContainer
        {
            public string SceneName;
            public RecipeActivator[] Activators;
            public Lifetime[] Lifetimes;
        }

        private Stack<SceneContainer> containers = new();
        private readonly InstanceCacheArray cacheArray;

        public SceneContainerStack(InstanceCacheArray cacheArray)
        {
            this.cacheArray = cacheArray ?? throw new ArgumentNullException(nameof(cacheArray));
        }

        public void Push(string sceneName, RecipeActivator[] activators, Lifetime[] lifetimes)
        {
            if (string.IsNullOrEmpty(sceneName))
                throw new ArgumentNullException(nameof(sceneName));
            if (activators == null)
                throw new ArgumentNullException(nameof(activators));
            if (lifetimes == null)
                throw new ArgumentNullException(nameof(lifetimes));

            containers.Push(new SceneContainer
            {
                SceneName = sceneName,
                Activators = activators,
                Lifetimes = lifetimes
            });
        }

        public void Pop(string sceneName)
        {
            if (containers.Count == 0)
                return;

            if (containers.Peek().SceneName == sceneName)
            {
                containers.Pop();
                cacheArray.ClearSceneSingletons();
                cacheArray.ClearAllCached();
            }
            else
            {
                List<SceneContainer> list = new(containers);
                int removed = list.RemoveAll(c => c.SceneName == sceneName);

                if (removed <= 0)
                    return;

                containers = new Stack<SceneContainer>(list.AsEnumerable().Reverse());
                cacheArray.ClearSceneSingletons();
                cacheArray.ClearAllCached();
            }
        }

        public bool TryResolveFromScene(int typeIdValue, out RecipeActivator activator, out Lifetime lifetime)
        {
            activator = null;
            lifetime = default;

            foreach (SceneContainer container in containers)
            {
                if (typeIdValue >= container.Activators.Length || container.Activators[typeIdValue] == null)
                    continue;

                activator = container.Activators[typeIdValue];
                lifetime = container.Lifetimes[typeIdValue];
                return true;
            }

            return false;
        }

        public RecipeActivator[] GetTopActivators()
            => containers.Count > 0 ? containers.Peek().Activators : null;

        public Lifetime[] GetTopLifetimes()
            => containers.Count > 0 ? containers.Peek().Lifetimes : null;

        public void Clear()
            => containers.Clear();

        public int Count => containers.Count;

        public string PrintDiagnostics()
        {
            System.Text.StringBuilder sb = new();
            sb.AppendLine($"[FastIoC] Scene Containers ({containers.Count} active):");

            if (containers.Count == 0)
            {
                sb.AppendLine("  (empty)");
                return sb.ToString();
            }

            int depth = 0;
            foreach (SceneContainer container in containers.Reverse())
            {
                sb.AppendLine($"  [{depth}] {container.SceneName}");
                depth++;
            }

            return sb.ToString();
        }
    }
}
