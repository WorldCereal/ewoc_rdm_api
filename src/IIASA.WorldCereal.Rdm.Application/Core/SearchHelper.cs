using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Core.Vector;
using IIASA.WorldCereal.Rdm.Dtos;
using IIASA.WorldCereal.Rdm.Entity;
using Microsoft.AspNetCore.Http;
using NetTopologySuite.Geometries;
using Volo.Abp.Domain.Repositories;

namespace IIASA.WorldCereal.Rdm.Core
{
    public static class SearchHelper
    {
        public static async Task<IQueryable<ItemEntity>> GetItemEntitiesQuery(ItemsRequestFilter filter,
            IRepository<ItemEntity, long> itemRepository)
        {
            Polygon boundingBox = null;

            if (filter.Bbox != null)
            {
                var geometryFactory = new GeometryFactory(new PrecisionModel(), GeoJsonHelper.GeometryWgs84Srid);
                boundingBox = new Polygon(new LinearRing(filter.GetBoundingBoxPoints()
                    .Select(x => new Coordinate(x.Latitude, x.Longitude)).ToArray()), geometryFactory);

                if (boundingBox.IsValid == false)
                {
                    throw new ArgumentException("Invalid Bounding box.");
                }
            }

            var query = await itemRepository.GetQueryableAsync();

            if (filter.EwocCodes.Any())
            {
                query = query.Where(x => filter.EwocCodes.Contains(x.EwocCode));
            }

            if (filter.ExtractTypes.Any())
            {
                query = query.Where(x => filter.ExtractTypes.Contains(x.Extract));
            }

            if (filter.H3Indexes.Any())
            {
                query = query.Where(x => filter.H3Indexes.Contains(x.H3Index));
            }

            if (filter.IrrigationTypes.Any())
            {
                query = query.Where(x => filter.IrrigationTypes.Contains(x.IrrigationStatus));
            }

            if (filter.LandCoverConfidence != default)
            {
                query = query.Where(x =>
                    x.LcQualityScore >= filter.LandCoverConfidence.Start &&
                    x.LcQualityScore <= filter.LandCoverConfidence.End);
            }

            if (filter.CropTypeConfidence != default)
            {
                query = query.Where(x =>
                    x.CtQualityScore >= filter.CropTypeConfidence.Start &&
                    x.CtQualityScore <= filter.CropTypeConfidence.End);
            }

            if (filter.ValidityStartTime.HasValue && filter.ValidityEndTime.HasValue &&
                filter.ValidityEndTime.Value > filter.ValidityStartTime.Value)
            {
                query = query.Where(x =>
                    x.ValidityTime >= filter.ValidityStartTime.Value && x.ValidityTime <= filter.ValidityEndTime);
            }

            if (boundingBox != null)
            {
                query = query.Where(x => x.Geometry.Within(boundingBox));
            }

            return query;
        }

        public static async Task<IQueryable<CollectionMetadataEntity>> GetCollectionQuery(EntitySearch filter,
            IRepository<CollectionMetadataEntity, Guid> collectionStore)
        {
            Polygon boundingBox = null;

            if (filter.Bbox != null)
            {
                var geometryFactory = new GeometryFactory(new PrecisionModel(), GeoJsonHelper.GeometryWgs84Srid);
                boundingBox = new Polygon(new LinearRing(filter.GetBoundingBoxPoints()
                    .Select(x => new Coordinate(x.Latitude, x.Longitude)).ToArray()), geometryFactory);

                if (boundingBox.IsValid == false)
                {
                    throw new ArgumentException("Invalid Bounding box.");
                }
            }

            var query = await collectionStore.GetQueryableAsync();

            if (filter.EwocCodes.Any())
            {
                query = query.Where(x => filter.EwocCodes.Any(y => x.EwocCodes.Contains(y)));
            }

            if (filter.IrrigationTypes.Any())
            {
                query = query.Where(x => filter.IrrigationTypes.Any(y => x.IrrTypes.Contains(y)));
            }

            if (filter.LandCoverConfidence != default)
            {
                query = query.Where(x =>
                    x.ConfidenceLandCover >= filter.LandCoverConfidence.Start &&
                    x.ConfidenceLandCover <= filter.LandCoverConfidence.End);
            }

            if (filter.CropTypeConfidence != default)
            {
                query = query.Where(x =>
                    x.ConfidenceCropType >= filter.CropTypeConfidence.Start &&
                    x.ConfidenceCropType <= filter.CropTypeConfidence.End);
            }

            if (filter.IrrigationConfidence != default)
            {
                query = query.Where(x =>
                    x.ConfidenceIrrigationType >= filter.IrrigationConfidence.Start &&
                    x.ConfidenceIrrigationType <= filter.IrrigationConfidence.End);
            }

            if (filter.ValidityTime.Start.HasValue && filter.ValidityTime.End.HasValue &&
                filter.ValidityTime.End.Value > filter.ValidityTime.Start.Value)
            {
                query = query.Where(x =>
                    (x.FirstDateOfValidityTime >= filter.ValidityTime.Start.Value &&
                     x.FirstDateOfValidityTime <= filter.ValidityTime.End) ||
                    (x.LastDateOfValidityTime >= filter.ValidityTime.Start.Value &&
                     x.LastDateOfValidityTime <= filter.ValidityTime.End));
            }

            if (boundingBox != null)
            {
                boundingBox.SRID = GeoJsonHelper.GeometryWgs84Srid;
                query = query.Where(x => boundingBox.Intersects(x.Extent));
            }

            return query;
        }

        public static string[] GetFiles(string directoryPath, string searchPatternComp)
        {
            var searchPatterns = searchPatternComp.Split("|").ToArray();
            var matchingFiles = new List<string>();
            foreach (var pattern in searchPatterns)
            {
                string[] files = Directory.GetFiles(directoryPath, pattern, SearchOption.AllDirectories);
                matchingFiles.AddRange(files);
            }

            return matchingFiles.ToArray();
        }


        public static void ZipFiles(string datasetPath, MemoryStream stream, string storageId = null)
        {
            var files = GetFiles(datasetPath, "*.shp|*.dbf|*.shx|*.prj|*.gpkg|*.geoparquet|*.parquet",
                SearchOption.AllDirectories);
            using (var zipArchive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                foreach (var fileName in files)
                {
                    var entryName = storageId + Path.GetExtension(fileName);

                    if (string.IsNullOrWhiteSpace(storageId))
                    {
                        entryName = Path.GetFileName(fileName);
                    }

                    zipArchive.CreateEntryFromFile(fileName, entryName);
                }
            }
        }

        public static string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            var searchPatterns = searchPattern.Split('|');
            var files = new List<string>();
            foreach (var pattern in searchPatterns)
            {
                files.AddRange(Directory.GetFiles(path, pattern, searchOption));
            }

            return files.ToArray();
        }

        public static string GetFilePath(string directoryToExtract)
        {
            var geoparquetFiles = GetFiles(directoryToExtract, "*.geoparquet|*.parquet");
            var gpkgFiles = GetFiles(directoryToExtract, "*.gpkg");
            var shapeFiles = GetFiles(directoryToExtract, "*.shp");
            string filePath;
            if (geoparquetFiles.Length > 0)
            {
                filePath = geoparquetFiles[0];
            }
            else if (gpkgFiles.Length > 0)
            {
                filePath = gpkgFiles[0];
            }
            else if (shapeFiles.Length > 0)
            {
                filePath = shapeFiles[0];
            }
            else
            {
                throw new BadHttpRequestException("File Type not Supported.");
            }

            return filePath;
        }
    }

    public static class DateParseHelper
    {
        const string FileName = "ValidityTimeAttributeInfo.json";

        public static async Task SaveAsync(string dateFromat, bool isManualDate, bool isValAttributeDateTimeType,
            string dirPath, string[] years)
        {
            var dateInfo = new DateInfo
            {
                DateFormat = dateFromat, IsManualDate = isManualDate,
                IsValAttributeDateTimeType = isValAttributeDateTimeType, ValAttributeYears = years
            };
            await Save(dirPath, dateInfo);
        }

        private static async Task Save(string dirPath, DateInfo dateInfo)
        {
            var jsonString = JsonSerializer.Serialize(dateInfo);
            await File.WriteAllTextAsync(Path.Combine(dirPath, FileName), jsonString);
        }

        public static async Task<DateInfo?> GetDateInfo(string dirPath)
        {
            var jsonString = await File.ReadAllTextAsync(Path.Combine(dirPath, FileName));
            return JsonSerializer.Deserialize<DateInfo>(jsonString);
        }

        public static async Task UpdateSelectedYear(string dirPath, int year)
        {
            var jsonString = await File.ReadAllTextAsync(Path.Combine(dirPath, FileName));
            var dateInfo = JsonSerializer.Deserialize<DateInfo>(jsonString);
            if (dateInfo != null)
            {
                dateInfo.SelectedYear = year;
                await Save(dirPath, dateInfo);
            }
        }
    }

    public class DateInfo
    {
        public string DateFormat { set; get; } = string.Empty;
        public bool IsManualDate { set; get; }
        public bool IsValAttributeDateTimeType { get; set; }
        public string[] ValAttributeYears { get; set; } = [];

        public int SelectedYear { get; set; } = -1;
    }
}