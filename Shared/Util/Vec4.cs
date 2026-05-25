using TrRebootTools.Shared.Serialization;

namespace TrRebootTools.Shared.Util
{
    public partial class Vec4 : IResourceStruct
    {
        public Vec4()
        {
        }

        public Vec4(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public float X;
        public float Y;
        public float Z;
        public float W;

        public override string ToString()
        {
            return $"({X}, {Y}, {Z}, {W})";
        }
    }
}
