using System.Collections.Generic;
using IIASA.WorldCereal.Rdm.Dtos.GeoJson;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.Mappings;

namespace IIASA.WorldCereal.Rdm.Core
{
    public interface IGeoJsonHelper
    {
        string GetFeature(ItemEntity itemEntity);
        string GetFeatureCollection(IEnumerable<ItemEntity> itemEntities);
        IEnumerable<ItemEntity> GetItems(FeatureCollectionGeoJSON featureCollection, string collectionId);
        //bool IsValid(object properties);
        string GetCollections(List<CollectionMapData> collections);
    }
}