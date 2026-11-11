using System.Runtime.CompilerServices;
using UnityEngine.Scripting;

// The generated injectors live in Assembly-CSharp (Assets/FastIoC/Generated) and assign
// `internal` fields of FastIoC MonoBehaviours (for example ProjectContext) directly, without reflection.
[assembly: InternalsVisibleTo("Assembly-CSharp")]

// Unity's linker drops an assembly that no code in the build references, even if it has
// [RuntimeInitializeOnLoadMethod] methods. Fast IoC starts from those methods, so its runtime
// assemblies must always be linked.
[assembly: AlwaysLinkAssembly]
