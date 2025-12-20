using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.Shared.Cdc
{
    /// <summary>
    /// Reference to a resource within a resource collection.
    /// </summary>
    /// <param name="CollectionReference">The archive file reference for the collection (.drm file)</param>
    /// <param name="ResourceIndex">Index of the resource within the collection</param>
    /// <param name="ResourceArchiveId">Archive ID where the resource data is stored (for priority selection)</param>
    public record ResourceCollectionItemReference(ArchiveFileReference CollectionReference, int ResourceIndex, int ResourceArchiveId = -1);
}
