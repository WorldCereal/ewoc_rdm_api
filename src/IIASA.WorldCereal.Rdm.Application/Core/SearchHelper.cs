using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using IIASA.WorldCereal.Rdm.Dtos;
using IIASA.WorldCereal.Rdm.Entity;
using NetTopologySuite.Geometries;
using Volo.Abp.Domain.Repositories;

namespace IIASA.WorldCereal.Rdm.Core
{
    public static class SearchHelper
    {
        public static IQueryable<ItemEntity> GetItemEntitiesQuery(ItemsRequestFilter filter,
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

            var query = itemRepository.AsQueryable();
            if (filter.LandCoverTypes.Any())
            {
                query = query.Where(x => filter.LandCoverTypes.Contains(x.Lc));
            }

            if (filter.CropTypes.Any())
            {
                query = query.Where(x => filter.CropTypes.Contains(x.Ct));
            }

            if (filter.IrrigationTypes.Any())
            {
                query = query.Where(x => filter.IrrigationTypes.Contains(x.Irr));
            }

            if (string.IsNullOrWhiteSpace(filter.Split) == false)
            {
                var splitEnum = SplitHelper.Get(filter.Split);
                query = query.Where(x => x.Split == splitEnum);
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

        public static IQueryable<CollectionMetadataEntity> GetCollectionQuery(ItemSearch filter,
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

            var query = collectionStore.AsQueryable();
            if (filter.LandCoverTypes.Any())
            {
                query = query.Where(x => filter.LandCoverTypes.Any(y => x.LandCovers.Contains(y)));
            }

            if (filter.LandCoverConfidence != default)
            {
                query = query.Where(x =>
                    x.ConfidenceLandCover >= filter.LandCoverConfidence.Start &&
                    x.ConfidenceLandCover <= filter.LandCoverConfidence.End);
            }

            if (filter.CropTypes.Any())
            {
                query = query.Where(x => filter.CropTypes.Any(y => x.CropTypes.Contains(y)));
            }

            if (filter.CropTypeConfidence != default)
            {
                query = query.Where(x =>
                    x.ConfidenceCropType >= filter.CropTypeConfidence.Start &&
                    x.ConfidenceCropType <= filter.CropTypeConfidence.End);
            }

            if (filter.IrrigationTypes.Any())
            {
                query = query.Where(x => filter.IrrigationTypes.Any(y => x.IrrTypes.Contains(y)));
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
                    x.FirstDateOfValidityTime >= filter.ValidityTime.Start.Value &&
                    x.FirstDateOfValidityTime <= filter.ValidityTime.End || 
                    x.LastDateOfValidityTime >= filter.ValidityTime.Start.Value &&
                    x.LastDateOfValidityTime <= filter.ValidityTime.End);
            }

            if (boundingBox != null)
            {
                query = query.Where(x => boundingBox.Intersects(x.Extent));
            }

            return query;
        }


        public static IQueryable<SampleEntity> GetSampleQuery(SampleSearchFilter filter,
            IRepository<SampleEntity, long> repository)
        {
            var geometryFactory = new GeometryFactory(new PrecisionModel(), GeoJsonHelper.GeometryWgs84Srid);
            var boundingBox = new Polygon(new LinearRing(filter.GetBoundingBoxPoints()
                .Select(x => new Coordinate(x.Latitude, x.Longitude)).ToArray()), geometryFactory);

            if (boundingBox.IsValid == false)
            {
                throw new ArgumentException("Invalid Bounding box.");
            }

            var query = repository.Where(x =>
                x.Geometry.Within(boundingBox) ||
                x.Geometry.Intersects(boundingBox)); // need to check if intersects is required.

            if (filter.Version.HasValue)
            {
                query = query.Where(x => Math.Abs(x.Version - filter.Version.Value) < 0.001);
            }

            if (string.IsNullOrWhiteSpace(filter.Split) == false)
            {
                var splitEnum = SplitHelper.Get(filter.Split);
                query = query.Where(x => x.Split == splitEnum);
            }

            if (filter.ValidityStartTime.HasValue && filter.ValidityEndTime.HasValue &&
                filter.ValidityEndTime.Value > filter.ValidityStartTime.Value)
            {
                query = query.Where(x =>
                    x.ValidityStartTime >= filter.ValidityStartTime.Value &&
                    x.ValidityEndTime <= filter.ValidityEndTime);
            }

            return query;
        }

        public static string[] GetFiles(string path, string searchPattern)
        {
            return Directory.GetFiles(path, searchPattern, SearchOption.AllDirectories);
        }
        
        public static void ZipFiles(string datasetPath, MemoryStream stream, string storageId = null)
        {
            var files = GetFiles(datasetPath, "*.shp|*.dbf|*.shx|*.prj|*.geojson|*.gpkg",
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
        
        private static string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            var searchPatterns = searchPattern.Split('|');
            var files = new List<string>();
            foreach (var pattern in searchPatterns)
            {
                files.AddRange(Directory.GetFiles(path, pattern, searchOption));
            }

            return files.ToArray();
        }
    }
}