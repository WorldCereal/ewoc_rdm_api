using System.Text.Json.Serialization;

namespace IIASA.WorldCereal.Rdm.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MetadataItemType
    {
        text = 0,
        link,
        integer,
        integerArray,
        decimals,
        email,
    }
}