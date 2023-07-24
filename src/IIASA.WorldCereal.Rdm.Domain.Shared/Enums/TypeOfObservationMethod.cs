using System.Text.Json.Serialization;

namespace IIASA.WorldCereal.Rdm.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TypeOfObservationMethod
    {
        Unknown,
        FieldObservationSurvey,
        FieldObservationSurveyWindshield,
        AutomatedClassification,
        ClassificationValidatedCrowd,
        ClassificationValidatedExpert,
        FormalDeclaration
    }
}