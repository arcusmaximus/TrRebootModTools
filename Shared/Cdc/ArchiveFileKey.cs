using System;

namespace TrRebootTools.Shared.Cdc
{
    public record struct ArchiveFileKey(ulong NameHash, ulong Locale) : IComparable<ArchiveFileKey>
    {
        public int CompareTo(ArchiveFileKey other)
        {
            int comparison = NameHash.CompareTo(other.NameHash);
            if (comparison != 0)
                return comparison;

            return Locale.CompareTo(other.Locale);
        }

        public override string ToString()
        {
            return $"{NameHash:X016}:{Locale:X016}";
        }
    }
}
