using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IIASA.WorldCereal.Rdm.Dtos.GeoJson;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.Enums;
using IIASA.WorldCereal.Rdm.Mappings;
using NetTopologySuite.Features;
using NetTopologySuite.IO;
using Newtonsoft.Json;
using Geometry = NetTopologySuite.Geometries.Geometry;

namespace IIASA.WorldCereal.Rdm.Core.Vector
{
    public class GeoJsonHelper : IGeoJsonHelper
    {
        private const string Title = "Title";
        private const string CollectionId = "CollectionId";
        private const string CollectionType = "CollectionType";
        private const string StoreType = "StoreType";
        private const string AccessType = "AccessType";
        private const string EwocCodes = "EwocCodes";
        private const string Extent = "Extent";
        private const string FeatureCount = "FeatureCount";
        public const int GeometryWgs84Srid = 4326;

        public const string EPsg4326EsriWkt =
            "GEOGCS[\"GCS_WGS_1984\",DATUM[\"D_WGS_1984\",SPHEROID[\"WGS_1984\",6378137,298.257223563]],PRIMEM[\"Greenwich\",0],UNIT[\"Degree\",0.017453292519943295]]";

        //private readonly List<string> _mandatoryPropNames = new() {SampleId, LandCover, CropType, Irrigation, ValidityTime, UserConf, Area};

        public static bool IsEpsg4326(string inputString)
        {
            var input = inputString.Replace(" ", string.Empty); // remove spaces.
            var regex = new Regex(
                "^GEOGCS\\[\"GCS_WGS_1984\",DATUM\\[\"D_WGS_1984\",SPHEROID\\[\"WGS_1984\",([+-]?(?=\\.\\d|\\d)(?:\\d+)?(?:\\.?\\d*))(?:[eE]([+-]?\\d+))?,([+-]?(?=\\.\\d|\\d)(?:\\d+)?(?:\\.?\\d*))(?:[eE]([+-]?\\d+))?]],PRIMEM\\[\"Greenwich\",([+-]?(?=\\.\\d|\\d)(?:\\d+)?(?:\\.?\\d*))(?:[eE]([+-]?\\d+))?],UNIT\\[\"Degree\",([+-]?(?=\\.\\d|\\d)(?:\\d+)?(?:\\.?\\d*))(?:[eE]([+-]?\\d+))?]]$",
                RegexOptions.CultureInvariant);
            if (regex.IsMatch(input) == false)
            {
                return false;
            }

            var values = new[] { "6378137", "298", "017453" };
            if (values.Any(x => input.Contains(x) == false))
            {
                return false;
            }

            if (input.Contains("\"Greenwich\",0") || input.Contains("\"Greenwich\",0.0"))
            {
                return true;
            }

            return false;
        }

        public string GetFeature(ItemEntity itemEntity)
        {
            var attTable = GetAttributesTable(itemEntity);

            var feature = new Feature(itemEntity.Geometry, attTable);
            return GetGeoJsonString(feature);
        }

        public string GetFeatureCollection(IEnumerable<ItemEntity> itemEntities)
        {
            var featureCollection = FeatureCollection(itemEntities);
            return GetGeoJsonString(featureCollection);
        }

        private static FeatureCollection FeatureCollection(IEnumerable<ItemEntity> itemEntities)
        {
            var featureCollection = new FeatureCollection();
            foreach (var itemEntity in itemEntities)
            {
                var attributesTable = GetAttributesTable(itemEntity);
                var feature = new Feature(itemEntity.Geometry, attributesTable);
                featureCollection.Add(feature);
            }

            return featureCollection;
        }

        public string GetCollections(List<CollectionMapData> collections)
        {
            var featureCollection = new FeatureCollection();
            foreach (var itemEntity in collections)
            {
                var attributesTable = GetAttTable(itemEntity);
                var feature = new Feature(itemEntity.Extent.Centroid, attributesTable);
                featureCollection.Add(feature);
            }

            return GetGeoJsonString(featureCollection);
        }

        private AttributesTable GetAttTable(CollectionMapData collection)
        {
            return new()
            {
                { CollectionId, collection.CollectionId },
                { Title, collection.Title },
                { FeatureCount, collection.FeatureCount },
                { CollectionType, collection.Type.ToString("G") },
                { StoreType, collection.StoreType.ToString("G") },
                { AccessType, collection.AccessType.ToString("G") },
                { EwocCodes, collection.EwocCodes },
                { Extent, collection.Extent.Coordinates.Select(x => new { x.X, x.Y }) }
            };
        }

        public IEnumerable<ItemEntity> GetItems(FeatureCollectionGeoJSON featureCollection, string collectionId)
        {
            var items = new List<ItemEntity>();
            foreach (var feature in featureCollection.Features)
            {
                var entity = new ItemEntity { Geometry = GetGeometry(JsonConvert.SerializeObject(feature.Geometry)) };

                var properties =
                    JsonConvert.DeserializeObject<IDictionary<string, object>>(feature.Properties.ToString());

                //TODO - check all mandatory properties
                entity.CollectionId = collectionId;
                entity.SampleId = properties[AttributeNames.SampleId].As<string>();
                entity.EwocCode = GetIntValue(properties, AttributeNames.EwocCode);
                entity.IrrigationStatus = GetIntValue(properties, AttributeNames.Irrigation);
                entity.ValidityTime = GetDate(properties[AttributeNames.ValidityTime]);
                //TODO check all non mandate props
                entity.ImageryDate = properties.ContainsKey(AttributeNames.ImageryTime)
                    ? GetDate(properties[AttributeNames.ImageryTime])
                    : default;
                entity.NumberOfValidations = properties.ContainsKey(AttributeNames.NumberOfValidations)
                    ? GetIntValue(properties, AttributeNames.NumberOfValidations)
                    : default;
                entity.TypeOfValidator = properties.ContainsKey(AttributeNames.TypeOfValidator)
                    ? (ValidatorType)GetIntValue(properties, AttributeNames.TypeOfValidator)
                    : default;
                entity.AgreementOfObservations = properties.ContainsKey(AttributeNames.AgreementOfObservations)
                    ? GetIntValue(properties, AttributeNames.AgreementOfObservations)
                    : default;
                entity.DisAgreementOfObservations = properties.ContainsKey(AttributeNames.DisagreementOfObservations)
                    ? GetIntValue(properties, AttributeNames.DisagreementOfObservations)
                    : default;
                items.Add(entity);
            }

            return items;
        }

        private static int GetIntValue(IDictionary<string, object> properties, string name)
        {
            return int.Parse(properties[name].ToString(), CultureInfo.InvariantCulture);
        }

        private static double GetDoubleValue(IDictionary<string, object> properties, string name)
        {
            return double.Parse(properties[name].ToString(), CultureInfo.InvariantCulture);
        }

        public static DateTime GetDate(object property)
        {
            return DateTime.ParseExact(property.As<string>(), "yyyy-M-d",
                CultureInfo.InvariantCulture);
        }

        private static ValidatorType? GetValidatorType(IAttributesTable attributes)
        {
            var value = GetValue<int?>(attributes, AttributeNames.TypeOfValidator);
            if (value == null)
            {
                return null;
            }

            return (ValidatorType)value;
        }

        private static T GetValue<T>(IAttributesTable attributes, string attributeName)
        {
            return (T)attributes.GetOptionalValue(attributeName);
        }

        private static AttributesTable GetAttributesTable(ItemEntity itemEntity)
        {
            var attTable = new AttributesTable
            {
                { AttributeNames.SampleId, itemEntity.SampleId },
                { AttributeNames.EwocCode, itemEntity.EwocCode },
                { AttributeNames.Irrigation, itemEntity.IrrigationStatus },
                { AttributeNames.ValidityTime, GetDate(itemEntity.ValidityTime) },
                { AttributeNames.Extract, itemEntity.Extract },
                { AttributeNames.LandCoverQualityScore, itemEntity.LcQualityScore },
                { AttributeNames.CropTypeQualityScore, itemEntity.CtQualityScore },
                { AttributeNames.H3Index, itemEntity.H3Index }
            };

            if (itemEntity.ImageryDate.HasValue && itemEntity.ImageryDate != default(DateTime))
            {
                attTable.Add(AttributeNames.ImageryTime, GetDate(itemEntity.ImageryDate));
            }

            if (itemEntity.TypeOfValidator.HasValue)
            {
                attTable.Add(AttributeNames.TypeOfValidator, itemEntity.TypeOfValidator.Value.ToString());
            }

            if (itemEntity.NumberOfValidations.HasValue)
            {
                attTable.Add(AttributeNames.NumberOfValidations, itemEntity.NumberOfValidations);
            }

            if (itemEntity.AgreementOfObservations.HasValue)
            {
                attTable.Add(AttributeNames.AgreementOfObservations, itemEntity.AgreementOfObservations);
            }

            if (itemEntity.DisAgreementOfObservations.HasValue)
            {
                attTable.Add(AttributeNames.DisagreementOfObservations, itemEntity.DisAgreementOfObservations);
            }

            return attTable;
        }


        private static string GetDate(DateTime? date)
        {
            return date.HasValue ? date.Value.ToString("yyyy-MM-dd") : string.Empty;
        }

        private static Geometry GetGeometry(string geometryString)
        {
            var serializer = GeoJsonSerializer.Create();
            using (var stringReader = new StringReader(geometryString))
            using (var jsonReader = new JsonTextReader(stringReader))
            {
                var geometry = serializer.Deserialize<Geometry>(jsonReader);
                geometry.SRID = GeometryWgs84Srid;
                if (geometry.IsValid == false)
                {
                    throw new InvalidOperationException($"Geometry is invalid with SRID-{GeometryWgs84Srid}");
                }

                return geometry;
            }
        }

        public static string GetGeoJsonString(object featureOrFeatureCollection)
        {
            var stringBuilder = new StringBuilder();
            var writer = new StringWriter(stringBuilder);
            var serializer = GeoJsonSerializer.Create();
            serializer.NullValueHandling = NullValueHandling.Ignore;
            serializer.Serialize(writer, featureOrFeatureCollection);
            writer.Flush();
            writer.Dispose();
            return stringBuilder.ToString();
        }
    }
}