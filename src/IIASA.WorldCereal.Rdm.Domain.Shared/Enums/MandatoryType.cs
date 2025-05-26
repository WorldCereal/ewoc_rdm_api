using System.Text.Json.Serialization;

namespace IIASA.WorldCereal.Rdm.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MandatoryType
    {
        None, // Not mandatory
        OnlyForPolygon, // attribute mandatory only for polygon dataset
        OnlyForPoint, // attribute mandatory only for point dataset
        PointAndPolygon,// attribute mandatory for both polygon and point dataset
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CodeType
    {
        LandCoverType,
        CropType,
        IrrigationType
    }
}