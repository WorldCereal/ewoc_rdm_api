using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace IIASA.WorldCereal.Rdm.Core
{
    public class VectorGeoPackageFileReader : VectorFileReader
    {
        private readonly SqliteConnection _connection;
        private readonly string _tableName;
        private readonly SqliteDataReader _reader;
        private readonly int _srsId;
        private readonly Envelope _envelope;

        public VectorGeoPackageFileReader(string path)
        {
            _connection = new SqliteConnection($"Data Source=\"{path}\"");

            _connection.Open();
            var tableNameCommand = _connection.CreateCommand();
            tableNameCommand.CommandText = @"SELECT * FROM gpkg_contents;";
            using (var tableReader = tableNameCommand.ExecuteReader())
            {
                while (tableReader.Read())
                {
                    _tableName = tableReader.GetString(tableReader.GetOrdinal("table_name"));
                    _srsId = tableReader.GetInt32(tableReader.GetOrdinal("srs_id"));

                    var xmin = tableReader.GetDouble(tableReader.GetOrdinal("min_x"));
                    var ymin = tableReader.GetDouble(tableReader.GetOrdinal("min_y"));
                    var xmax = tableReader.GetDouble(tableReader.GetOrdinal("max_x"));
                    var ymax = tableReader.GetDouble(tableReader.GetOrdinal("max_y"));

                    var polygon = new Polygon(new LinearRing(new List<Coordinate>
                    {
                        new() {X = xmin, Y = ymin},
                        new() {X = xmax, Y = ymin},
                        new() {X = xmax, Y = ymax},
                        new() {X = xmin, Y = ymax},
                        new() {X = xmin, Y = ymin}
                    }.ToArray()));

                    _envelope = polygon.EnvelopeInternal;
                }
            }

            var entityCommand = _connection.CreateCommand();
            entityCommand.CommandText = $@"SELECT * FROM [{_tableName}];";
            _reader = entityCommand.ExecuteReader();
        }

        public override void Dispose()
        {
            _connection.Close();
            _reader.Dispose();
            _connection.Dispose();
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
            return _envelope;
        }

        public override ShapeGeometryType GetShapeGeometryType()
        {
            var tableNameCommand = _connection.CreateCommand();
            tableNameCommand.CommandText = "SELECT * FROM gpkg_geometry_columns";
            var tableReader = tableNameCommand.ExecuteReader();
            var type = "";
            while (tableReader.Read())
            {
                type = tableReader.GetString(tableReader.GetOrdinal("geometry_type_name"));
            }

            if (type == "POINT")
            {
                return ShapeGeometryType.Point;
            }

            if (type == "POLYGON" || type == "MULTIPOLYGON")
            {
                return ShapeGeometryType.Polygon;
            }

            throw new InvalidDataException($"Invalid geometry type while reading ShapeGeometryType");
        }

        public override int GetOrdinal(string columnName)
        {
            return _reader.GetOrdinal(columnName);
        }

        public override string[] GetAllFieldName()
        {
            var count = _reader.FieldCount;
            var names = new List<string>();
            for (int i = 0; i < count; i++)
            {
                names.Add(_reader.GetName(i));
            }

            return names.ToArray();
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
            return _srsId;
        }

        public override Geometry Geometry
        {
            get
            {
                var geometryReader = new GeoPackageGeoReader();
                return geometryReader.Read(_reader.GetStream(_reader.GetOrdinal("geom")));
            }
        }
    }
}