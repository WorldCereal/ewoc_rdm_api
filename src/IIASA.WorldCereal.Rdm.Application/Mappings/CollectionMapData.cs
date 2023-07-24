using IIASA.WorldCereal.Rdm.Enums;
using NetTopologySuite.Geometries;

namespace IIASA.WorldCereal.Rdm.Mappings
{
    public class CollectionMapData
    {
        public string CollectionId { get; set; } // tenant name
        public string Title { get; set; }
        public Geometry Extent { get; set; }
        public CollectionType Type { get; set; }
        public StoreType StoreType { get; set; }
        public AccessType AccessType { get; set; }
        public long FeatureCount { get; set; }
    }
}