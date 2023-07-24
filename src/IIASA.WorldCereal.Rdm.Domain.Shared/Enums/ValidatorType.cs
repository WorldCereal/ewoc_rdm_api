using System.Text.Json.Serialization;

namespace IIASA.WorldCereal.Rdm.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ValidatorType
    {
        NonExpert = 0,
        Expert
    }
}