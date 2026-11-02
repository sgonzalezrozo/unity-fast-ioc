using System;

namespace FastIoC.Core
{
    public readonly struct TypeId : IEquatable<TypeId>
    {
        public readonly int Id;

        public TypeId(int id)
        {
            Id = id;
        }

        public bool Equals(TypeId other) => Id == other.Id;
        public override bool Equals(object obj) => obj is TypeId other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();
        public override string ToString() => $"TypeId({Id})";

        public static bool operator ==(TypeId left, TypeId right) => left.Equals(right);
        public static bool operator !=(TypeId left, TypeId right) => !left.Equals(right);
    }
}

