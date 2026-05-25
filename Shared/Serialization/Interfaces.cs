namespace TrRebootTools.Shared.Serialization
{
    public interface IResourceCompositeType
    {
    }

    public interface IResourceStruct : IResourceCompositeType
    {
        void Read(ResourceReader reader);
        void Write(ResourceBuilder writer);
    }

    public interface IResourceStruct32 : IResourceStruct
    {
    }

    public interface IResourceStruct64 : IResourceStruct
    {
    }

    public interface IResourceUnion : IResourceCompositeType
    {
        void Read(ResourceReader reader, int fieldSelector);
        void Write(ResourceBuilder writer, int fieldSelector);
    }

    public interface IResourceUnion32 : IResourceUnion
    {
    }

    public interface IResourceUnion64 : IResourceUnion
    {
    }
}
