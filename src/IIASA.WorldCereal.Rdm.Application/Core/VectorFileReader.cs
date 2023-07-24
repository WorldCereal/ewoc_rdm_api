using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace IIASA.WorldCereal.Rdm.Core
{
    public abstract class VectorFileReader : IDisposable
    {
        public abstract bool Read();
        public abstract object GetValue(int index);
        public abstract Envelope GetBounds();
        public abstract ShapeGeometryType GetShapeGeometryType();
        public abstract int GetOrdinal(string columnName);
        public abstract string[] GetAllFieldName();
        public abstract int GetInt32(int index);
        public abstract double GetDouble(int index);
        public abstract string GetString(int index);
        public abstract DateTime GetDateTime(int index);
        public abstract int GetSrsId();

        public static VectorFileReader GetReader(string path)
        {
            if (path.Contains(".shp"))
            {
                return new VectorShapeFileReader(path);
            }

            if (path.Contains(".gpkg"))
            {
                return new VectorGeoPackageFileReader(path);
            }

            throw new InvalidOperationException($"Not supported to read-{path}");
        }

        public abstract void Dispose();
        public abstract Geometry Geometry { get; }
    }
}