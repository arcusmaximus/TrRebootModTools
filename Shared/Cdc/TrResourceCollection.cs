using System;
using System.IO;
using System.Runtime.InteropServices;

namespace TrRebootTools.Shared.Cdc
{
    internal abstract class TrResourceCollection<TResourceLocation, TLocale> : ResourceCollection<
        TrResourceCollection<TResourceLocation, TLocale>.ResourceCollectionHeader,
        TrResourceCollection<TResourceLocation, TLocale>.ResourceIdentification,
        TResourceLocation,
        TLocale
    >
        where TResourceLocation : unmanaged
        where TLocale : unmanaged
    {
        protected TrResourceCollection(ulong nameHash, ulong locale, Stream stream)
            : base(nameHash, locale, stream)
        {
        }

        protected override ResourceKey ToResourceKey(ResourceIdentification identification)
        {
            ulong locale;
            if (identification.Locale is uint uintLocale)
                locale = 0xFFFFFFFF00000000 | uintLocale;
            else if (identification.Locale is ulong ulongLocale)
                locale = ulongLocale;
            else
                throw new NotSupportedException();

            return new ResourceKey(
                (ResourceType)identification.Type,
                (ResourceSubType)identification.SubType,
                identification.Id,
                locale
            );
        }

        protected override void UpdateResourceIdentification(ref ResourceIdentification identification, ResourceReference resourceRef)
        {
            if (resourceRef.RefDefinitionsSize != null)
            {
                identification.RefDefinitionsSize = resourceRef.RefDefinitionsSize.Value;
                identification.BodySize = resourceRef.BodySize;
            }
            else
            {
                identification.BodySize = resourceRef.BodySize - identification.RefDefinitionsSize;
            }
            if (resourceRef.Enabled)
                identification.Type = (byte)(resourceRef.Type < ResourceType.Max ? resourceRef.Type : ResourceType.CollisionModel);
            else
                identification.Type = (byte)ResourceType.Empty;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ResourceCollectionHeader : IHeader
        {
            public int Version;
            public int IncludeLength;
            public int DependenciesLength;
            public int PaddingLength;
            public int Size;
            public int Flags;
            public int NumResources;
            public int MainResourceIndex;

            int IHeader.Version
            {
                get => Version;
                set => Version = value;
            }

            int IHeader.IncludeLength
            {
                get => IncludeLength;
                set => IncludeLength = value;
            }

            int IHeader.DependenciesLength
            {
                get => DependenciesLength;
                set => DependenciesLength = value;
            }

            int IHeader.NumResources
            { 
                get => NumResources;
                set => NumResources = value;
            }

            int IHeader.MainResourceIndex
            {
                get => MainResourceIndex;
                set => MainResourceIndex = value;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ResourceIdentification
        {
            public uint BodySize;
            public byte Type;
            public byte Flags;
            public short Padding;
            public uint SubTypeAndRefDefinitionsSize;
            public int Id;
            public TLocale Locale;

            public int SubType
            {
                get => (int)((SubTypeAndRefDefinitionsSize & 0xFF) >> 1);
                set => SubTypeAndRefDefinitionsSize = (SubTypeAndRefDefinitionsSize & 0x7FFFFF01) | (uint)(value << 1);
            }

            public uint RefDefinitionsSize
            {
                get => SubTypeAndRefDefinitionsSize >> 8;
                set => SubTypeAndRefDefinitionsSize = (SubTypeAndRefDefinitionsSize & 0xFF) | (value << 8);
            }
        }
    }
}
