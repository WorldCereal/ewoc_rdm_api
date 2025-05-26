using System.Text.Json;
using Parquet;

namespace IIASA.WorldCereal.Rdm.Core.Vector.GeoParquet;

public static class GeoParquetExtensions
{
    public static string? GeoMetadataString(this ParquetReader parquetFileReader)
    {
        var metadata = parquetFileReader.Metadata?.KeyValueMetadata;
        var geoMetaData = metadata?.Find(x=>x.Key == "geo")?.Value;
        return geoMetaData;
    }

    public static GeoParquet? GetGeoParquetMetadata(this ParquetReader parquetFileReader)
    {
        var geoMetaData = parquetFileReader.GeoMetadataString();
        if(geoMetaData != null)
        {
            return JsonSerializer.Deserialize<GeoParquet>(geoMetaData, JsonSerializerOptions.Default);
        }
        return null;
    }
}