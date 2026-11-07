using System.Runtime.CompilerServices;

// The Unity adapters (FastIoC assembly) reuse the pooled component buffer used by IInstantiator.
[assembly: InternalsVisibleTo("FastIoC")]
