using System.Runtime.CompilerServices;
using UnityEngine.Scripting;

// The Unity adapters (FastIoC assembly) reuse the pooled component buffer used by IInstantiator.
[assembly: InternalsVisibleTo("FastIoC")]

// Unity's linker drops an assembly that no code in the build references, even if it has
// [RuntimeInitializeOnLoadMethod] methods. Fast IoC starts from those methods, so its runtime
// assemblies must always be linked.
[assembly: AlwaysLinkAssembly]
