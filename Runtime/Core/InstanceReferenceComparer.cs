using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FastIoC.Core
{
    /// <summary>
    /// Compares objects by reference, so tracking an instance never calls user Equals or GetHashCode.
    /// </summary>
    internal sealed class InstanceReferenceComparer : IEqualityComparer<object>
    {
        public static readonly InstanceReferenceComparer Instance = new();

        public new bool Equals(object x, object y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
