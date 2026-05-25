namespace TrRebootTools.Shared.Cdc
{
    public class ResourceRef
    {
        public ResourceRef(int offset = 0)
        {
            Offset = offset;
        }

        public ResourceRef(ResourceKey? externalResource, int offset = 0)
        {
            ExternalResource = externalResource;
            Offset = offset;
        }

        public ResourceRef(ResourceType type, int id, int offset = 0)
        {
            ExternalResource = new(type, id);
            Offset = offset;
        }

        public ResourceKey? ExternalResource
        {
            get;
        }

        public bool IsInternal => ExternalResource == null;

        public bool IsExternal => ExternalResource != null;

        public int Offset
        {
            get;
            set;
        }

        public static implicit operator ResourceRef(ResourceKey resourceKey)
        {
            return new(resourceKey);
        }

        public static ResourceRef operator+(ResourceRef resourceRef, int offset)
        {
            return new(resourceRef.ExternalResource, resourceRef.Offset + offset);
        }
    }

    public class ResourceRef<T> : ResourceRef
    {
        public ResourceRef(int offset = 0)
            : base(offset)
        {
        }

        public T Value
        {
            get;
            set;
        }
    }
}
