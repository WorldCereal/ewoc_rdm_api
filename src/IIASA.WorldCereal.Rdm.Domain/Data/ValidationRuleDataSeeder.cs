using System.Collections.Generic;
using System.IO;
using System.Linq;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.Enums;

namespace IIASA.WorldCereal.Rdm.Data
{
    public static class ValidationRuleDataSeeder
    {
        public static IEnumerable<ValidationRule> Get()
        {
            var list = new List<ValidationRule>
            {
                GetSampleIdRules(),
                GetEwocRules(),
                GetIrrRules(),
                GetValidityTimeRules(),
                GetOptionalImageryTimeRules(),
                GetOptionalAgreementOfObsRules(),
                GetOptionalDisagreementOfObsRules(),
                GetOptionalNumberOfValidationsRules(),
                GetOptionalTypeOfValidatorRules()
            };

            return list;
        }

        public static EoType[] GetEoTypes()
        {
            var lcTypes = File.ReadAllLines("rdm_lcCodes.txt").Select(x => GetEoType(x, CodeType.LandCoverType))
                .ToArray();
            var ctTypes = File.ReadAllLines("rdm_ctCodes.txt").Select(x => GetEoType(x, CodeType.CropType)).ToArray();
            var irrTypes = File.ReadAllLines("rdm_irrCodes.txt").Select(x => GetEoType(x, CodeType.IrrigationType))
                .ToArray();

            return new[] { lcTypes, ctTypes, irrTypes }.SelectMany(x => x).Where(x => x.Value != -1).ToArray();
        }

        private static EoType GetEoType(string line, CodeType type)
        {
            var values = line.Split(",");
            if (long.TryParse(values[0], out var res))
            {
                return new EoType { CodeType = type, Name = values[1], Value = res };
            }

            return new EoType { CodeType = type, Name = values[1], Value = -1 };
        }

        private static ValidationRule GetValidityTimeRules()
        {
            var validationRule = new ValidationRule
            {
                AttributeName = AttributeNames.ValidityTime, // Need to change
                MandatoryType = MandatoryType.PointAndPolygon,
                MissingErrorMessage = $"Validity Time ({AttributeNames.ValidityTime}) attribute is missing in dataset.",
                InvalidValueErrorMessage =
                    $"Validity Time ({AttributeNames.ValidityTime}) attribute has invalid value(s). Attribute should be a DateTime Object with year starting from 2017 till present date. Future dates are not allowed.",
                ValueChecks = [ValueCheckType.Date]
            };

            return validationRule;
        }

        private static ValidationRule GetSampleIdRules()
        {
            var validationRule = new ValidationRule
            {
                AttributeName = AttributeNames.SampleId,
                MandatoryType = MandatoryType.PointAndPolygon,
                MissingErrorMessage = $"{AttributeNames.SampleId} attribute is missing in dataset.",
                InvalidValueErrorMessage = $"{AttributeNames.SampleId} attribute has invalid value(s)",
                ValueChecks = [ValueCheckType.Text, ValueCheckType.Unique]
            };

            return validationRule;
        }

        private static ValidationRule GetEwocRules()
        {
            var validationRule = new ValidationRule
            {
                AttributeName = AttributeNames.EwocCode,
                MandatoryType = MandatoryType.PointAndPolygon,
                MissingErrorMessage = $"{AttributeNames.EwocCode} attribute is missing in dataset.",
                InvalidValueErrorMessage = $"{AttributeNames.EwocCode} attribute has invalid value(s)",
                ValueChecks = [ValueCheckType.BigInteger]
            };

            var values = File.ReadAllLines("rdm_ctCodes.txt").Select(x => x.Split(",").First()).Select(long.Parse);

            var validValues = values.Select(x => new RuleValidValue
                { Name = AttributeNames.EwocCode, Value = x.ToString() }).ToList();
            validationRule.RuleValidValues = validValues;
            return validationRule;
        }

        private static ValidationRule GetIrrRules()
        {
            var validationRule = new ValidationRule
            {
                AttributeName = AttributeNames.Irrigation,
                MandatoryType = MandatoryType.PointAndPolygon,
                MissingErrorMessage = $"{AttributeNames.Irrigation} attribute is missing in dataset.",
                InvalidValueErrorMessage = $"{AttributeNames.Irrigation} attribute has invalid value(s)",
                ValueChecks = [ValueCheckType.Integer]
            };

            var values = new[] { 0, 100, 200, 210, 213, 214, 215, 220, 223, 224, 225 };
            var validValues = values.Select(x => new RuleValidValue
                { Name = AttributeNames.Irrigation, Value = x.ToString() }).ToList();
            validationRule.RuleValidValues = validValues;
            return validationRule;
        }

        private static ValidationRule GetOptionalImageryTimeRules()
        {
            var validationRule = new ValidationRule
            {
                AttributeName = AttributeNames.ImageryTime,
                MandatoryType = MandatoryType.None,
                MissingErrorMessage = "Imagery Time (Image_time) optional attribute is missing in dataset.",
                InvalidValueErrorMessage =
                    "Optional Imagery Time (Image_time) attribute has invalid value(s), Date should be in YYYY-MM-DD format as a string data.",
                ValueChecks = [ValueCheckType.Date]
            };

            return validationRule;
        }

        private static ValidationRule GetOptionalNumberOfValidationsRules()
        {
            var validationRule = new ValidationRule
            {
                AttributeName = AttributeNames.NumberOfValidations,
                MandatoryType = MandatoryType.None,
                MissingErrorMessage =
                    $"Number Of Validations ({AttributeNames.NumberOfValidations}) optional attribute is missing in dataset.",
                InvalidValueErrorMessage =
                    $"Optional number Of Validations ({AttributeNames.NumberOfValidations}) attribute supports non negative whole number, uploaded dataset has invalid value(s)",
                ValueChecks = [ValueCheckType.Integer]
            };

            return validationRule;
        }

        private static ValidationRule GetOptionalAgreementOfObsRules()
        {
            var validationRule = new ValidationRule
            {
                AttributeName = AttributeNames.AgreementOfObservations,
                MandatoryType = MandatoryType.None,
                MissingErrorMessage = "Agreement Of Observations (agreement) optional attribute is missing in dataset.",
                InvalidValueErrorMessage =
                    "Optional agreement Of Observations (agreement) attribute supports non negative whole number, uploaded dataset has invalid value(s)",
                ValueChecks = [ValueCheckType.Integer]
            };

            return validationRule;
        }

        private static ValidationRule GetOptionalDisagreementOfObsRules()
        {
            var validationRule = new ValidationRule
            {
                AttributeName = AttributeNames.DisagreementOfObservations,
                MandatoryType = MandatoryType.None,
                MissingErrorMessage =
                    "Disagreement Of Observations (dagreement) optional attribute is missing in dataset.",
                InvalidValueErrorMessage =
                    "Optional Disagreement Of Observations (dagreement) attribute supports non negative whole number, uploaded dataset has invalid value(s)",
                ValueChecks = [ValueCheckType.Integer]
            };

            return validationRule;
        }

        private static ValidationRule GetOptionalTypeOfValidatorRules()
        {
            var validationRule = new ValidationRule
            {
                AttributeName = AttributeNames.TypeOfValidator,
                MandatoryType = MandatoryType.None,
                MissingErrorMessage =
                    $"Type Of Validator ({AttributeNames.TypeOfValidator}) optional attribute is missing in dataset.",
                InvalidValueErrorMessage =
                    $"Optional Type Of Validator ({AttributeNames.TypeOfValidator}) attribute valid values are [Expert,NonExpert,Both] case sensitive. Uploaded dataset has invalid value(s)",
                ValueChecks = [ValueCheckType.Text]
            };

            var validValues = new[] { "Expert", "NonExpert", "Both" };
            validationRule.RuleValidValues = validValues
                .Select(x => new RuleValidValue { Name = AttributeNames.TypeOfValidator, Value = x }).ToList();

            return validationRule;
        }
    }
}