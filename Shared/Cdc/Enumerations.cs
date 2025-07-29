namespace TrRebootTools.Shared.Cdc
{
    public enum CdcGame
    {
        Tr2013 = 9,
        Rise,
        Shadow
    }

    public enum ResourceType
    {
        Unknown = 0,
        Empty = 1,
        Animation = 2,
        PoseSpaceDeformer = 4,
        Texture = 5,
        SoundBank = 6,
        Dtp = 7,
        Script = 8,
        ShaderLib = 9,
        Material = 10,
        GlobalContentReference = 11,
        Model = 12,
        CollisionMesh = 13,
        ObjectReference = 14,
        AnimationLib = 15,
        Max
    }

    public enum ResourceSubType
    {
        Texture = 5,
        Model = 26,
        ModelData = 27,
        ShResource = 116,
        CubeLut = 117,

        // Custom
        Level = 9998,
        StreamLayer = 9999
    }

    public enum ShaderType
    {
        Pixel,
        Vertex
    }
}
