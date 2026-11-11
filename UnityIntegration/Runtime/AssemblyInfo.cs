using UnityEngine.Scripting;

// Unity's linker drops an assembly that no code in the build references, even if it has
// [RuntimeInitializeOnLoadMethod] methods. Fast IoC starts from those methods, so this assembly
// (FastIoCUnityBootstrapper) must always be linked.
[assembly: AlwaysLinkAssembly]
