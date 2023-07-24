using System;
using System.Linq;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace IIASA.WorldCereal.Rdm.Core
{
    public class VectorShapeFileReader : VectorFileReader
    {
        private readonly ShapefileDataReader _reader;

        public VectorShapeFileReader(string path)
        {
            _reader = new ShapefileDataReader(path,
                new GeometryFactory(new PrecisionModel(), GeoJsonHelper.GeometryWgs84Srid));
        }

        public override bool Read()
        {
            return _reader.Read();
        }

        public override object GetValue(int index)
        {
            return _reader.GetValue(index);
        }

        public override Envelope GetBounds()
        {
            return _reader.ShapeHeader.Bounds;
        }

        public override ShapeGeometryType GetShapeGeometryType()
        {
            return _reader.ShapeHeader.ShapeType;
        }

        public override int GetOrdinal(string columnName)
        {
            return _reader.GetOrdinal(columnName);
        }

        public override string[] GetAllFieldName()
        {
            return _reader.DbaseHeader.Fields.Select(x => x.Name).ToArray();
        }

        public override int GetInt32(int index)
        {
            return _reader.GetInt32(index);
        }

        public override double GetDouble(int index)
        {
            return _reader.GetDouble(index);
        }

        public override string GetString(int index)
        {
            return _reader.GetString(index);
        }

        public override DateTime GetDateTime(int index)
        {
            return _reader.GetDateTime(index);
        }

        public override int GetSrsId()
        {
            return GeoJsonHelper.GeometryWgs84Srid;
        }

        public override void Dispose()
        {
            _reader.Dispose();
        }

        public override Geometry Geometry => _reader.Geometry;
    }
}