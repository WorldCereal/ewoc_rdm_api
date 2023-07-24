using System;
using System.Collections.Generic;
using System.Linq;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Dtos;
using NetTopologySuite.Geometries;

namespace IIASA.WorldCereal.Rdm.Mappings
{
    public static class SpatialMappingHelper
    {
        public static ExtentDto GetExtent(Geometry extent, DateTime firstDateOfValidityTime,
            DateTime lastDateOfValidityTime)
        {
            Temporal temporal = new Temporal
            {
                Interval = new[] {new List<DateTime?> {firstDateOfValidityTime, lastDateOfValidityTime}}
            };
            var xCoordinates = extent.Coordinates.Select(x => x.X).ToList().OrderBy(x => x);
            var yCoordinates = extent.Coordinates.Select(x => x.Y).ToList().OrderBy(x => x);

            Spatial spatial = new Spatial
            {
                Bbox = new[]
                    {new[] {xCoordinates.First(), yCoordinates.First(), xCoordinates.Last(), yCoordinates.Last()}}
            };

            return new ExtentDto {Spatial = spatial, Temporal = temporal};
        }

        public static Polygon GetGeometry(IEnumerable<CoordinateDto> bbox)
        {
            var geometryFactory = new GeometryFactory(new PrecisionModel(), GeoJsonHelper.GeometryWgs84Srid);
            var polygon = new Polygon(new LinearRing(bbox
                .Select(x => new Coordinate(x.Latitude, x.Longitude)).ToArray()), geometryFactory);
            return polygon;
        }

        public static IEnumerable<CoordinateDto> GetCoordinates(Coordinate[] extentCoordinates)
        {
            return extentCoordinates.Select(c => new CoordinateDto {Latitude = c.X, Longitude = c.Y});
        }

        public static string GetLowerLeftCoordinates(Geometry extent)
        {
            var xmin = extent.Coordinates.Select(x => x.X).ToList().OrderBy(v => v).First();
            var ymin = extent.Coordinates.Select(x => x.Y).ToList().OrderBy(v => v).First();

            return $"{xmin};{ymin}";
        }

        public static string GetUpperRightCoordinates(Geometry extent)
        {
            var xmax = extent.Coordinates.Select(x => x.X).ToList().OrderByDescending(v => v).First();
            var ymax = extent.Coordinates.Select(x => x.Y).ToList().OrderByDescending(v => v).First();

            return $"{xmax};{ymax}";
        }
    }
}