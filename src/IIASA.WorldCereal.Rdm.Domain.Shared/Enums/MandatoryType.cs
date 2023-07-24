using System.Text.Json.Serialization;

namespace IIASA.WorldCereal.Rdm.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MandatoryType
    {
        None, // Not mandatory
        OnlyForPolygon, // attribute mandatory only for polygon dataset
        OnlyForPoint,
        PointAndPolygon,// attribute mandatory for both polygon and point dataset
    }
}