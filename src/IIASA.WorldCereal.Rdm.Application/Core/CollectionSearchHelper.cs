using System.Linq;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.Enums;

namespace IIASA.WorldCereal.Rdm.Core
{
    public static class CollectionSearchHelper
    {
        public static bool RbacFilter(CollectionMetadataEntity dataset, AccessType[] communityAllowedAccess, AccessType[] consortiumAllowedAccess)
        {
            return (dataset.StoreType == StoreType.Community && communityAllowedAccess.Contains(dataset.AccessType)) ||
                   (dataset.StoreType == StoreType.Reference && consortiumAllowedAccess.Contains(dataset.AccessType));
        }
    }
}