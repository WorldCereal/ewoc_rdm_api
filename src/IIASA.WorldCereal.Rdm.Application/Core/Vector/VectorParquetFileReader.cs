using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IIASA.WorldCereal.Rdm.Core.Vector.GeoParquet;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Parquet;
using Parquet.Rows;
using Parquet.Schema;

namespace IIASA.WorldCereal.Rdm.Core.Vector;

public class VectorParquetFileReader : VectorFileReader
{
    private readonly ParquetReader _reader;
    private readonly FileStream _stream;
    private readonly GeoParquet.GeoParquet? _metadata;
    private readonly Envelope? _envelope;
    private readonly IReadOnlyList<Field> _schemaFields;
    private int _currentRowIndex = 0;
    private Table? _table = null;
    private int _rowCount;
    private int _geometryColIndex;
    private WKBReader _wkbReader;
    private DataField[] _dataFields;

    public VectorParquetFileReader(string filePath)
    {
        _stream = File.OpenRead(filePath);
        var options = new ParquetOptions { TreatByteArrayAsString = false };
        _reader = ParquetReader.CreateAsync(_stream, options).GetAwaiter().GetResult();
        _metadata = _reader.GetGeoParquetMetadata();

        var bbox = _metadata?.Columns.First().Value.Bbox;

        var xmin = bbox![0];
        var ymin = bbox![1];
        var xmax = bbox![2];
        var ymax = bbox![3];

        var geometryFactory = new GeometryFactory(new PrecisionModel(), GeoJsonHelper.GeometryWgs84Srid);
        var polygon = new Polygon(new LinearRing(new List<Coordinate>
        {
            new() { X = xmin, Y = ymin },
            new() { X = xmax, Y = ymin },
            new() { X = xmax, Y = ymax },
            new() { X = xmin, Y = ymax },
            new() { X = xmin, Y = ymin }
        }.ToArray()), geometryFactory);

        _envelope = polygon.EnvelopeInternal;

        _schemaFields = _reader.Schema.Fields;

        _dataFields = _reader.Schema.DataFields;

        _currentRowIndex = 0;

        _wkbReader = new WKBReader();
        _geometryColIndex = GetColIndex("geometry"); // name of column is fixed for geoparquet files by OGC standards
    }

    public override void Dispose()
    {
        _reader.Dispose();
        _stream.Dispose();
    }

    public override bool Read()
    {
        if (_table == null)
        {
            _table = _reader.ReadAsTableAsync().GetAwaiter().GetResult();
            _rowCount = _table.Count;
            _currentRowIndex = 0;
            return _currentRowIndex < _rowCount;
        }

        _currentRowIndex++;
        return _currentRowIndex < _rowCount;
    }

    public override object GetValue(int index)
    {
        return _table[_currentRowIndex].Get<object>(index);
    }

    public override Envelope GetBounds()
    {
        return _envelope;
    }

    public override ShapeGeometryType GetShapeGeometryType()
    {
        var geometryType = _metadata.Columns.First().Value.Geometry_types;
        if (geometryType.Contains("Polygon") || geometryType.Contains("MultiPolygon"))
        {
            return ShapeGeometryType.Polygon;
        }

        if (geometryType.Contains("Point"))
        {
            return ShapeGeometryType.Point;
        }

        throw new InvalidDataException($"Invalid geometry type while reading ShapeGeometryType");
    }

    public override int GetOrdinal(string columnName)
    {
        return GetColIndex(columnName);
    }

    private int GetColIndex(string columnName)
    {
        var schemaFieldsCount = _schemaFields.Count;
        for (int i = 0; i < schemaFieldsCount; i++)
        {
            if (_schemaFields[i].Name == columnName)
            {
                return i;
            }
        }

        return -1;
    }

    public override string[] GetAllFieldName()
    {
        return _schemaFields.Select(x => x.Name).ToArray();
    }

    public override int GetInt32(int index)
    {
        try
        {
            return _table![_currentRowIndex].GetInt(index);
        }
        catch (Exception)
        {
            var stringValue = _table![_currentRowIndex].GetString(index);
            return int.Parse(stringValue);
        }
    }

    public override long GetInt64(int index)
    {
        return _table![_currentRowIndex].GetLong(index);
    }

    public override double GetDouble(int index)
    {
        return _table![_currentRowIndex].GetDouble(index);
    }

    public override string GetString(int index)
    {
        return _table![_currentRowIndex].GetString(index);
    }

    public override DateTime GetDateTime(int index)
    {
        try
        {
            return _table![_currentRowIndex].GetDateTime(index);
        }
        catch (Exception e)
        {
            // for parquet datetime can be stored in int64
            var longVal = _table![_currentRowIndex].GetLong(index);
            var milliseconds = longVal/1000000;
            var datev= DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).DateTime;
            return datev;
        }
    }

    public override int GetSrsId()
    {
        var srId = _metadata?.Columns.First().Value.Crs?.Id?.Code;
        if (srId != null)
        {
            return srId.Value;
        }

        if (_envelope != null && _geometryColIndex != -1
                              && _envelope.MinX >= -180 && _envelope.MinY >= -90 &&
                              _envelope.MaxX <= 180 && _envelope.MaxY <= 90)
        {
            return 4326; // for parqut files without crs, check geo column and bbox and set to EPSG4326 as per spec
        }

        return 0;
    }

    public override Type GetFieldType(int fieldIndex)
    {
        return _dataFields[fieldIndex].ClrType;
    }

    public override Geometry Geometry
    {
        get
        {
            var wkb = _table![_currentRowIndex].GetByteArray(_geometryColIndex);
            return _wkbReader.Read(wkb);
        }
    }
}