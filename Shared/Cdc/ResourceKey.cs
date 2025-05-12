namespace TrRebootTools.Shared.Cdc
{
    public struct ResourceKey
    {
        public ResourceKey(ResourceType type, int id)
        {
            Type = type;
            SubType = 0;
            Id = id;
        }

        public ResourceKey(ResourceType type, ResourceSubType subType, int id)
        {
            Type = type;
            SubType = subType;
            Id = id;
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

        public override readonly bool Equals(object obj)
        {
            return obj is ResourceKey other && other.Type == Type && other.Id == Id;
        }

        public override readonly int GetHashCode()
        {
            return (int)Type | (Id << 8);
        }

        public override string ToString()
        {
            return $"{Type}:{Id}";
        }
    }
}
