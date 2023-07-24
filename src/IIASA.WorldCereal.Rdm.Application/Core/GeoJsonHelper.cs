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

namespace IIASA.WorldCereal.Rdm.Core
{
    public class GeoJsonHelper : IGeoJsonHelper
    {
        private const string Title = "Title";
        private const string CollectionId = "CollectionId";
        private const string CollectionType = "CollectionType";
        private const string StoreType = "StoreType";
        private const string AccessType = "AccessType";
        private const string Extent = "Extent";
        private const string FeatureCount = "FeatureCount";
        private const string SampleId = "sampleID";
        private const string Id = "Id";
        private const string Version = "version";
        private const string LandCover = "LC";
        private const string CropType = "CT";
        private const string Irrigation = "IRR";
        private const string ValidityTime = "valtime";
        private const string ValidityStartTime = "valStartTime";
        private const string ValidityEndTime = "valEndTime";
        private const string UserConf = "userconf";
        private const string Area = "area";
        private const string NoOfValidations = "numberval";
        private const string AgreementOfObservations = "agreement";
        private const string DisAgreementOfObservations = "dagreement";
        private const string Split = "split";
        private const string ImageryTime = "imtime";
        private const string TypeOfValidator = "typeval";
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

            var values = new[] {"6378137", "298", "017453"};
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

        public static void WriteDataset(IEnumerable<ItemEntity> itemEntities, string tempPath, string collectionId)
        {
            Directory.CreateDirectory(tempPath);
            var shapeFileName = Path.Combine(tempPath, collectionId);
            var features = FeatureCollection(itemEntities).ToArray();
            var writer = new ShapefileDataWriter(shapeFileName)
            {
                Header = ShapefileDataWriter.GetHeader(features.First(), features.Length)
            };
            writer.Write(features);
            File.WriteAllText($"{shapeFileName}.prj", EPsg4326EsriWkt);
        }

        public string GetFeatureCollection(IEnumerable<SampleEntity> itemEntities)
        {
            var featureCollection = new FeatureCollection();
            foreach (var itemEntity in itemEntities)
            {
                var attributesTable = GetSampleAttTable(itemEntity);
                var feature = new Feature(itemEntity.Geometry, attributesTable);
                featureCollection.Add(feature);
            }

            return GetGeoJsonString(featureCollection);
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

        private AttributesTable GetAttTable(CollectionMapData itemEntity)
        {
            return new()
            {
                {CollectionId, itemEntity.CollectionId},
                {Title, itemEntity.Title},
                {FeatureCount, itemEntity.FeatureCount},
                {CollectionType, itemEntity.Type.ToString("G")},
                {StoreType, itemEntity.StoreType.ToString("G")},
                {AccessType, itemEntity.AccessType.ToString("G")},
                {Extent, itemEntity.Extent.Coordinates.Select(x => new {x.X, x.Y})}
            };
        }

        private static AttributesTable GetSampleAttTable(SampleEntity itemEntity)
        {
            return new()
            {
                {Id, itemEntity.Id},
                {Version, itemEntity.Version},
                {Split, itemEntity.Split},
                {ValidityStartTime, itemEntity.ValidityStartTime},
                {ValidityEndTime, itemEntity.ValidityEndTime}
            };
        }

        public IEnumerable<ItemEntity> GetItems(FeatureCollectionGeoJSON featureCollection, string collectionId)
        {
            var items = new List<ItemEntity>();
            foreach (var feature in featureCollection.Features)
            {
                var entity = new ItemEntity {Geometry = GetGeometry(JsonConvert.SerializeObject(feature.Geometry))};

                var properties =
                    JsonConvert.DeserializeObject<IDictionary<string, object>>(feature.Properties.ToString());

                //TODO - check all mandatory properties
                entity.CollectionId = collectionId;
                entity.SampleId = properties[SampleId].As<string>();
                entity.Lc = GetIntValue(properties, LandCover);
                entity.Ct = GetIntValue(properties, CropType);
                entity.Irr = GetIntValue(properties, Irrigation);
                entity.UserConf = GetIntValue(properties, UserConf);
                entity.Area = GetDoubleValue(properties, Area);
                entity.ValidityTime = GetDate(properties[ValidityTime]);
                //TODO check all non mandate props
                entity.Split = GetSplitValue(properties);
                entity.ImageryTime = properties.ContainsKey(ImageryTime) ? GetDate(properties[ImageryTime]) : default;
                entity.NumberOfValidations = properties.ContainsKey(NoOfValidations)
                    ? GetIntValue(properties, NoOfValidations)
                    : default;
                entity.TypeOfValidator = properties.ContainsKey(TypeOfValidator)
                    ? (ValidatorType) GetIntValue(properties, TypeOfValidator)
                    : default;
                entity.AgreementOfObservations = properties.ContainsKey(AgreementOfObservations)
                    ? GetIntValue(properties, AgreementOfObservations)
                    : default;
                entity.DisAgreementOfObservations = properties.ContainsKey(DisAgreementOfObservations)
                    ? GetIntValue(properties, DisAgreementOfObservations)
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

        private static SplitType GetSplitValue(IDictionary<string, object> properties)
        {
            if (properties.ContainsKey(Split))
            {
                var value = properties[Split].As<string>();
                return SplitHelper.Get(value);
            }

            return SplitType.Test;
        }

        public IEnumerable<SampleEntity> GetSamples(FeatureCollectionGeoJSON featureCollection, double version)
        {
            var samples = new List<SampleEntity>();
            foreach (var feature in featureCollection.Features)
            {
                var properties =
                    JsonConvert.DeserializeObject<IDictionary<string, object>>(feature.Properties.ToString());
                var sample = new SampleEntity
                {
                    Geometry = GetGeometry(JsonConvert.SerializeObject(feature.Geometry)),
                    Version = version,
                    Split = GetSplitValue(properties),
                    ValidityStartTime = GetDate(properties[ValidityStartTime]),
                    ValidityEndTime = GetDate(properties[ValidityEndTime])
                };

                samples.Add(sample);
            }

            return samples;
        }


        public static DateTime GetDate(object property)
        {
            return DateTime.ParseExact(property.As<string>(), "yyyy-M-d",
                CultureInfo.InvariantCulture);
        }

        private static ValidatorType? GetValidatorType(IAttributesTable attributes)
        {
            var value = GetValue<int?>(attributes, TypeOfValidator);
            if (value == null)
            {
                return null;
            }

            return (ValidatorType) value;
        }

        private static T GetValue<T>(IAttributesTable attributes, string attributeName)
        {
            return (T) attributes.GetOptionalValue(attributeName);
        }

        private static AttributesTable GetAttributesTable(ItemEntity itemEntity)
        {
            var attTable = new AttributesTable();
            attTable.Add(SampleId, itemEntity.SampleId);
            attTable.Add(LandCover, itemEntity.Lc);
            attTable.Add(CropType, itemEntity.Ct);
            attTable.Add(Irrigation, itemEntity.Irr);
            attTable.Add(ValidityTime, GetDate(itemEntity.ValidityTime));
            attTable.Add(Split, itemEntity.Split.ToString("G").ToUpperInvariant());
            if (itemEntity.UserConf != 0)
            {
                attTable.Add(UserConf, itemEntity.UserConf);
            }

            if (itemEntity.Area != 0.0)
            {
                attTable.Add(Area, itemEntity.Area);
            }

            if (itemEntity.ImageryTime.HasValue &&  itemEntity.ImageryTime != default(DateTime))
            {
                attTable.Add(ImageryTime, GetDate(itemEntity.ImageryTime));
            }

            if (itemEntity.TypeOfValidator.HasValue)
            {
                attTable.Add(TypeOfValidator, itemEntity.TypeOfValidator.Value.ToString());
            }

            if (itemEntity.NumberOfValidations.HasValue)
            {
                attTable.Add(NoOfValidations, itemEntity.NumberOfValidations);
            }

            if (itemEntity.AgreementOfObservations.HasValue)
            {
                attTable.Add(AgreementOfObservations, itemEntity.AgreementOfObservations);
            }

            if (itemEntity.DisAgreementOfObservations.HasValue)
            {
                attTable.Add(DisAgreementOfObservations, itemEntity.DisAgreementOfObservations);
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