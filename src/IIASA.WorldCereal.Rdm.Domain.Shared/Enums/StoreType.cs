using System.Text.Json.Serialization;

namespace IIASA.WorldCereal.Rdm.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum StoreType
    {
        Reference, // consortium datasets
        Community // user uploaded datasets
    }
}
