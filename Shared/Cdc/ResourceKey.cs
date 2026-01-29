namespace TrRebootTools.Shared.Cdc
{
    public struct ResourceKey
    {
        public ResourceKey(ResourceType type, int id)
        {
            Type = type;
            SubType = 0;
            Id = id;
            Locale = 0xFFFFFFFFFFFFFFFF;
        }

        public ResourceKey(ResourceType type, ResourceSubType subType, int id, ulong locale)
        {
            Type = type;
            SubType = subType;
            Id = id;
            Locale = locale;
        }

        public ResourceType Type
        {
            get;
        }

        public ResourceSubType SubType
        {
            get;
        }

        public int Id
        {
            get;
        }

        public ulong Locale
        {
            get;
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is ResourceKey other && other.Type == Type && other.Id == Id && other.Locale == Locale;
        }

        public override readonly int GetHashCode()
        {
            return (int)Type | (Id << 8);
        }

        public override string ToString()
        {
            string result = $"{Type}:{Id}";
            if (Locale != 0xFFFFFFFFFFFFFFFF)
                result += $":{Locale:X016}";

            return result;
        }
    }
}
