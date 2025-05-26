using System.Text.Json.Serialization;

namespace IIASA.WorldCereal.Rdm.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ValueCheckType
    {
        Integer,
        Decimal,
        Date,
        Text,
        Unique,
        BigInteger,
    }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum EventType
    {
        SubmittedForReview,
        ReviewInProgress,
        NeedsFix,
        VerifiedAndAccepted,
        Public
    }
}