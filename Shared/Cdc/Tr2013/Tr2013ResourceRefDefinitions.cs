using System.IO;

namespace TrRebootTools.Shared.Cdc.Tr2013
{
    internal class Tr2013ResourceRefDefinitions : ResourceRefDefinitions
    {
        public Tr2013ResourceRefDefinitions(ResourceReference resourceRef, Stream stream, bool readSizeOnly)
            : base(resourceRef, stream, readSizeOnly)
        {
        }

        protected override int WideExternalRefSize => 8;

        public override ResourceKey? GetExternalRefTarget(int refPos)
        {
            ResourceKey? resourceKey = base.GetExternalRefTarget(refPos);
            if (resourceKey == null || _resourceRef == null)
                return resourceKey;

            return Tr2013ResourceCollection.AdjustResourceKeyAfterRead(_resourceRef.ArchiveId, resourceKey.Value);
        }

        public override void SetExternalRefTarget(int refPos, ResourceKey resourceKey)
        {
            if (_resourceRef != null)
                resourceKey = Tr2013ResourceCollection.AdjustResourceKeyBeforeWrite(_resourceRef.ArchiveId, resourceKey);

            base.SetExternalRefTarget(refPos, resourceKey);
        }
    }
}
