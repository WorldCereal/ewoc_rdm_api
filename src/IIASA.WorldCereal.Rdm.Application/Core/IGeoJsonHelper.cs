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
        string GetFeatureCollection(IEnumerable<SampleEntity> itemEntities);
        IEnumerable<ItemEntity> GetItems(FeatureCollectionGeoJSON featureCollection, string collectionId);
        //bool IsValid(object properties);
        IEnumerable<SampleEntity> GetSamples(FeatureCollectionGeoJSON featureCollection, double version);
        string GetCollections(List<CollectionMapData> collections);
    }
}