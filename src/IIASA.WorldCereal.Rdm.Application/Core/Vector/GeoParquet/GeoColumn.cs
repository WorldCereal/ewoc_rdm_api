using System.Collections.Generic;

namespace IIASA.WorldCereal.Rdm.Core.Vector.GeoParquet;

public partial class GeoColumn
{
    [System.Text.Json.Serialization.JsonPropertyName("crs")]
    [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
    public Crs? Crs { get; set; }
}

/*public class Crs
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public Id? Id { get; set; }
}*/

public class Id
{
    [System.Text.Json.Serialization.JsonPropertyName("authority")]
    public string? Authority { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("code")]
    public int Code { get; set; }
}

// Root myDeserializedClass = JsonSerializer.Deserialize<Root>(myJsonResponse);
public class Axis
{
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string Name { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("direction")]
    public string Direction { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("unit")]
    public string Unit { get; set; }
}

public class Bbox
{
    [System.Text.Json.Serialization.JsonPropertyName("south_latitude")]
    public int SouthLatitude { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("west_longitude")]
    public int WestLongitude { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("north_latitude")]
    public int NorthLatitude { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("east_longitude")]
    public int EastLongitude { get; set; }
}

public class CoordinateSystem
{
    [System.Text.Json.Serialization.JsonPropertyName("subtype")]
    public string Subtype { get; set; } = "ellipsoidal";

    [System.Text.Json.Serialization.JsonPropertyName("axis")]
    public List<Axis> Axis { get; set; } =
    [
        new Axis
        {
            Name = "Geodetic latitude",
            Abbreviation = "Lat",
            Direction = "north",
            Unit = "degree"
        },
        new Axis
        {
            Name = "Geodetic longitude",
            Abbreviation = "Lon",
            Direction = "east",
            Unit = "degree"
        }
    ];
}

public class DatumEnsemble
{
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string Name { get; set; } = "World Geodetic System 1984 ensemble";

    [System.Text.Json.Serialization.JsonPropertyName("members")]
    public List<Member> Members { get; set; } =
    [
        new Member
        {
            Name = "World Geodetic System 1984 (Transit)"
        },
        new Member
        {
            Name = "World Geodetic System 1984 (G730)"
        },
        new Member
        {
            Name = "World Geodetic System 1984 (G873)"
        },
        new Member
        {
            Name = "World Geodetic System 1984 (G1150)"
        },
        new Member
        {
            Name = "World Geodetic System 1984 (G1674)"
        },
        new Member
        {
            Name = "World Geodetic System 1984 (G1762)"
        },
        new Member
        {
            Name = "World Geodetic System 1984 (G2139)"
        }
    ];

    [System.Text.Json.Serialization.JsonPropertyName("ellipsoid")]
    public Ellipsoid Ellipsoid { get; set; } = new Ellipsoid()
    {
        InverseFlattening = 298.257223563,
        Name = "WGS 84",
        SemiMajorAxis = 6378137
    };

    [System.Text.Json.Serialization.JsonPropertyName("accuracy")]
    public string Accuracy { get; set; } = "2.0";

    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public Id Id { get; set; } = new() { Authority = "EPSG", Code = 6326 };
}

public class Ellipsoid
{
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string Name { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("semi_major_axis")]
    public int SemiMajorAxis { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("inverse_flattening")]
    public double InverseFlattening { get; set; }
}

public class Member
{
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string Name { get; set; }
}

public class Crs
{
    [System.Text.Json.Serialization.JsonPropertyName("$schema")]
    public string Schema { get; set; } = "https://proj.org/schemas/v0.7/projjson.schema.json";

    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string Type { get; set; } = "GeographicCRS";

    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string Name { get; set; } = "WGS 84";

    [System.Text.Json.Serialization.JsonPropertyName("datum_ensemble")]
    public DatumEnsemble DatumEnsemble { get; set; } = new();

    [System.Text.Json.Serialization.JsonPropertyName("coordinate_system")]
    public CoordinateSystem CoordinateSystem { get; set; } = new();

    [System.Text.Json.Serialization.JsonPropertyName("scope")]
    public string Scope { get; set; } = "Horizontal component of 3D system.";

    [System.Text.Json.Serialization.JsonPropertyName("area")]
    public string Area { get; set; } = "World.";

    [System.Text.Json.Serialization.JsonPropertyName("bbox")]
    public object Bbox { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public Id Id { get; set; } = new Id()
    {
        Authority = "EPSG",
        Code = 4326
    };
}