using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IIASA.WorldCereal.Rdm.Core.Vector.GeoParquet;

public static class GeoMetadata
{
    public static Dictionary<string, string> GetGeoMetadata(GeoColumn geoColumn)
    {
        var parquet = new GeoParquet
        {
            Version = "1.0.0",
            Primary_column = "geometry"
        };
        parquet.Columns.Add("geometry", geoColumn);

        JsonSerializerOptions options = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        var json = JsonSerializer.Serialize(parquet, options);
        var dictionary = new Dictionary<string, string>
        {
            ["geo"] = json
        };
        return dictionary;
    }
}