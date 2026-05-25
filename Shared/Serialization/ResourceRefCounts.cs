using System.Runtime.InteropServices;

namespace TrRebootTools.Shared.Serialization
{
    [StructLayout(LayoutKind.Sequential)]
    internal partial struct ResourceRefCounts : IResourceStruct
    {
        public int NumInternalRefs;
        public int NumWideExternalRefs;
        public int NumIntPatches;
        public int NumShortPatches;
        public int NumPackedExternalRefs;
    }
}
