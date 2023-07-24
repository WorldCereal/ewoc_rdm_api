using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeoAPI.CoordinateSystems;
using IIASA.Db.UpdateTools.core;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Entity;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json;
using Npgsql.Bulk;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using AttributesTable = NetTopologySuite.Features.AttributesTable;
using TCoordinate = GeoAPI.Geometries.Coordinate;

namespace IIASA.Db.UpdateTools.jobs
{
    public class SampleJobs
    {
        private static readonly IEnumerable<WktString> ListOfWkt = SridReader.GetSrids();
        private static readonly ICoordinateSystem Wgs84CoordinateSystem = new CoordinateSystemFactory().CreateFromWkt(ListOfWkt.First(x => x.WktId == 4326).Wkt);

        public void GenerateSamples(string filePath)
        {
            var featureCollection = GetFeatureCollection(filePath);

            var sampleList = new List<PatchSampleData>();
            var count = 1;
            foreach (var feature in featureCollection)
            {
                var attributes = feature.Attributes;
                var item = new PatchSampleData();
                
                var epsg = GetIntValue(attributes.GetOptionalValue("epsg"));

                var bounds = attributes.GetOptionalValue("bounds");
                item.Geometry = GetWgs84Bounds(bounds, epsg);
                item.StartDate = GeoJsonHelper.GetDate(attributes.GetOptionalValue("start_date"));
                item.EndDate = GeoJsonHelper.GetDate(attributes.GetOptionalValue("end_date"));
                var value = attributes.GetOptionalValue("split");
                if(value == null)
                {
                    continue;
                }

                item.Split = value.ToString();
                var optionalValue = attributes.GetOptionalValue("location_id");
                item.SampleId = optionalValue == null ? Guid.NewGuid().ToString("N") : optionalValue.ToString();
                sampleList.Add(item);

                Console.WriteLine($"{count++}.SampleId- {item.SampleId}, bounds-{bounds} StartDate-{item.StartDate}, EndData-{item.EndDate}, Split-{item.Split}");
            }

            SaveToFile(sampleList, ".\\patchSamples.geojson");
        }

        public void UploadSamplesToDb(string geoJsonPath, Configuration configuration, double version)
        {
            var sampleEntities = new List<SampleEntity>();
            var featureCollection = GetFeatureCollection(geoJsonPath);
            var index = 1;
            foreach (var feature in featureCollection)
            {
                var attributes = feature.Attributes;
                var sampleEntity = new SampleEntity
                {
                    Split = SplitHelper.Get(attributes.GetOptionalValue("split").As<string>()),
                    ValidityStartTime = GeoJsonHelper.GetDate(attributes.GetOptionalValue("startDate")),
                    ValidityEndTime = GeoJsonHelper.GetDate(attributes.GetOptionalValue("endDate")),
                    Version = version,
                    Geometry = feature.Geometry
                };
                sampleEntities.Add(sampleEntity);

                Console.WriteLine($"Reading record- {index++}");
            }

            Console.WriteLine($"Inserting record into DB - {index}");
            var context = new UpdaterContext(configuration, true);
            var bulkUploader = new NpgsqlBulkUploader(context);
            bulkUploader.Import(sampleEntities);
            Console.WriteLine($"Completed!");
        }

        private static FeatureCollection GetFeatureCollection(string filePath)
        {
            var geoJsonString = File.ReadAllText(filePath, Encoding.UTF8);
            var serializer = GeoJsonSerializer.Create();
            using var stringReader = new StringReader(geoJsonString);
            using var jsonReader = new JsonTextReader(stringReader);
            var featureCollection = serializer.Deserialize<FeatureCollection>(jsonReader);
            return featureCollection;
        }

        private void SaveToFile(List<PatchSampleData> sampleList, string fileName)
        {
            var featureCollection = new FeatureCollection();
            foreach (var data in sampleList)
            {
                var feature = new Feature {Geometry = data.Geometry};
                var attributes = new AttributesTable
                {
                    {"sampleID", data.SampleId},
                    {"startDate", data.StartDate.ToString("yyyy-MM-dd")},
                    {"endDate", data.StartDate.ToString("yyyy-MM-dd")},
                    {"split", data.Split}
                };
                feature.Attributes = attributes;
                featureCollection.Add(feature);
            }

            File.WriteAllText(fileName, GeoJsonHelper.GetGeoJsonString(featureCollection), Encoding.UTF8);
        }


        private Geometry GetWgs84Bounds(object value, int epsg)
        {
            if (value == null)
            {
                throw  new Exception("Invalid BBox value");
            }

            var values = value.ToString().Replace("(", String.Empty)
                .Replace(")", String.Empty)
                .Split(",");

            

            var xmin = int.Parse(values[0]);
            var ymin = int.Parse(values[1]);
            var llCoordinate = GetWgs84Coordinate(xmin, ymin, epsg);

            var xmax = int.Parse(values[2]);
            var ymax = int.Parse(values[3]);
            var urCoordinate = GetWgs84Coordinate(xmax, ymax, epsg);

            return new Polygon(new LinearRing(new[]
            {
                new Coordinate(llCoordinate.X, llCoordinate.Y),
                new Coordinate(llCoordinate.X, urCoordinate.Y),
                new Coordinate(urCoordinate.X, urCoordinate.Y),
                new Coordinate(urCoordinate.X, llCoordinate.Y),
                new Coordinate(llCoordinate.X, llCoordinate.Y),
            }));
        }

        private int GetIntValue(object value)
        {
            if (value == null)
            {
                throw new Exception("Invalid Epsg code");
            }

            return int.Parse(value.ToString());
        }

        private static TCoordinate GetWgs84Coordinate(double xLat, double yLong, int sourceSrid)
        {
            var source = new CoordinateSystemFactory().CreateFromWkt(ListOfWkt.First(x => x.WktId == sourceSrid).Wkt);
            var transformation = new CoordinateTransformationFactory().CreateFromCoordinateSystems(source, Wgs84CoordinateSystem);
            var value = transformation.MathTransform.Transform(new TCoordinate(xLat, yLong));
            return value;
        }
    }

    public class PatchSampleData
    {
        public string SampleId { get; set; }

        public string Split { get; set; }

        public Geometry Geometry { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public struct WktString
    {
        /// <summary>
        /// Well-known ID
        /// </summary>
        public int WktId;
        /// <summary>
        /// Well-known Text
        /// </summary>
        public string Wkt;
    }

    internal class SridReader
    {
        private static readonly Lazy<CoordinateSystemFactory> CoordinateSystemFactory =
            new Lazy<CoordinateSystemFactory>(() => new CoordinateSystemFactory());
        
        /// <summary>
        /// Enumerates all SRID's in the SRID.csv file.
        /// </summary>
        /// <returns>Enumerator</returns>
        public static IEnumerable<WktString> GetSrids(string filename = ".\\SRID.csv")
        {
            var stream = File.OpenRead(filename);

            using (var sr = new StreamReader(stream, Encoding.UTF8))
            {
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    int split = line.IndexOf(';');
                    if (split <= -1) continue;

                    var wkt = new WktString
                    {
                        WktId = int.Parse(line.Substring(0, split)),
                        Wkt = line.Substring(split + 1)
                    };
                    yield return wkt;
                }
            }
        }

        /// <summary>
        /// Gets a coordinate system from the SRID.csv file
        /// </summary>
        /// <param name="id">EPSG ID</param>
        /// <param name="file">(optional) path to CSV File with WKT definitions.</param>
        /// <returns>Coordinate system, or <value>null</value> if no entry with <paramref name="id"/> was not found.</returns>
        public static ICoordinateSystem GetCSbyId(int id, string file = null)
        {
            //ICoordinateSystemFactory factory = new CoordinateSystemFactory();
            foreach (var wkt in GetSrids(file))
                if (wkt.WktId == id)
                {
                    return CoordinateSystemFactory.Value.CreateFromWkt(wkt.Wkt);
                }

            return null;
        }
    }
}
