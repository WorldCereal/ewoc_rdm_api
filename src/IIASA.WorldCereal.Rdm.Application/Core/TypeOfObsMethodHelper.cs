using System.Collections.Generic;
using IIASA.WorldCereal.Rdm.Enums;

namespace IIASA.WorldCereal.Rdm.Core
{
    public static class TypeOfObsMethodHelper
    {
        private static readonly Dictionary<string, TypeOfObservationMethod> _map = 
            new()
            {
                {TypeOfObservationMethod.Unknown.ToString("G").ToLowerInvariant(),TypeOfObservationMethod.Unknown},
                {TypeOfObservationMethod.AutomatedClassification.ToString("G").ToLowerInvariant(),TypeOfObservationMethod.AutomatedClassification},
                {TypeOfObservationMethod.FormalDeclaration.ToString("G").ToLowerInvariant(),TypeOfObservationMethod.FormalDeclaration},
                {TypeOfObservationMethod.ClassificationValidatedCrowd.ToString("G").ToLowerInvariant(),TypeOfObservationMethod.ClassificationValidatedCrowd},
                {TypeOfObservationMethod.ClassificationValidatedExpert.ToString("G").ToLowerInvariant(),TypeOfObservationMethod.ClassificationValidatedExpert},
                {TypeOfObservationMethod.FieldObservationSurvey.ToString("G").ToLowerInvariant(),TypeOfObservationMethod.FieldObservationSurvey},
                {TypeOfObservationMethod.FieldObservationSurveyWindshield.ToString("G").ToLowerInvariant(),TypeOfObservationMethod.FieldObservationSurveyWindshield}
            };
        public static TypeOfObservationMethod Get(string value)
        {
            return _map[value.ToLowerInvariant()];
        }
    }
}