using NetTopologySuite.Geometries;

namespace IIASA.WorldCereal.Rdm.Core.Vector
{
    public static class VectorHelper
    {
        public static Coordinate[] GetCoordinates(Envelope bounds)
        {
            var xmin = bounds.MinX;
            var ymin = bounds.MinY;
            var xmax = bounds.MaxX;
            var ymax = bounds.MaxY;

            return new Coordinate[]
            {
                new() {X = xmin, Y = ymin},
                new() {X = xmax, Y = ymin},
                new() {X = xmax, Y = ymax},
                new() {X = xmin, Y = ymax},
                new() {X = xmin, Y = ymin}
            };
        }

        public static Coordinate[] GetFromMinMax(double xmin, double ymin, double xmax, double ymax)
        {
            return new Coordinate[]
            {
                new() {X = xmin, Y = ymin},
                new() {X = xmax, Y = ymin},
                new() {X = xmax, Y = ymax},
                new() {X = xmin, Y = ymax},
                new() {X = xmin, Y = ymin}
            };
        }

        public static GeometryFactory GeometryFactory()
        {
            return new GeometryFactory(new PrecisionModel(), GeoJsonHelper.GeometryWgs84Srid);
        }
    }
}