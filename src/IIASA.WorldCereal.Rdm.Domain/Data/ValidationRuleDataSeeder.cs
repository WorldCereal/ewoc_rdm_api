using System.Collections.Generic;
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
                GetLcRules(),
                GetCtRules(),
                GetIrrRules(),
                GetSampleIdRules(),
                GetUserConfRules(),
                GetValidityTimeRules(),
                GetAreaRules(),

                //optional attributes
                GetSplitRules(),
                GetOptionalImageryTimeRules(),
                GetOptionalSupportingRadiusRules(),
                GetOptionalAgreementOfObsRules(),
                GetOptionalDisagreementOfObsRules(),
                GetOptionalNumberOfValidationsRules(),
                GetOptionalTypeOfValidatorRules()
            };

            return list;
        }

        private static ValidationRule GetSplitRules()
        {
            var validationRule = new ValidationRule
            {
                AttributeName = AttributeNames.Split,
                MandatoryType = MandatoryType.None,
                MissingErrorMessage = "optional split attribute is missing in dataset.",
                InvalidValueErrorMessage =
                    "Optional split attribute valid values-[CAL,VAL,TEST] case sensitive. Uploaded dataset has invalid value(s)",
                ValueChecks = new[] {ValueCheckType.Text}
            };
            var validValues = new[] {"CAL", "VAL", "TEST"};
            validationRule.RuleValidValues =
                validValues.Select(x => new RuleValidValue {Name = "split", Value = x}).ToList();
            return validationRule;
        }

        private static ValidationRule GetAreaRules()
        {
            var validationRule = new ValidationRule
            {
                AttributeName = AttributeNames.Area,
                MandatoryType = MandatoryType.None,
                MissingErrorMessage = "optional area attribute is missing in dataset.",
                InvalidValueErrorMessage = "Optional area has invalid value(s)",
                ValueChecks = new[] {ValueCheckType.Decimal}
            };

            return validationRule;
        }

        private static ValidationRule GetValidityTimeRules()
        {
            var validationRule = new ValidationRule
            {
                AttributeName = AttributeNames.ValidityTime, // Need to change
                MandatoryType = MandatoryType.PointAndPolygon,
                MissingErrorMessage = "Validity Time (valtime) attribute is missing in dataset.",
                InvalidValueErrorMessage =
                    "Validity Time (valtime) attribute has invalid value(s), Date should be in YYYY-MM-DD format as a string data.",
                ValueChecks = new[] {ValueCheckType.Date}
            };

            return validationRule;
        }

        private static ValidationRule GetUserConfRules()
        {
            var validationRule = new ValidationRule
            {
                AttributeName = AttributeNames.UserConf,
                MandatoryType = MandatoryType.None,
                MissingErrorMessage = "optional userconf attribute is missing in dataset.",
                InvalidValueErrorMessage = "Optional userconf attribute has invalid value(s)",
                ValueChecks = new[] {ValueCheckType.Decimal}
            };

            return validationRule;
        }

        private static ValidationRule GetSampleIdRules()
        {
            var validationRule = new ValidationRule
            {
                AttributeName = AttributeNames.SampleId,
                MandatoryType = MandatoryType.PointAndPolygon,
                MissingErrorMessage = "sampleID attribute is missing in dataset.",
                InvalidValueErrorMessage = "sampleID attribute has invalid value(s)",
                ValueChecks = new[] {ValueCheckType.Text, ValueCheckType.Unique}
            };

            return validationRule;
        }

        private static ValidationRule GetCtRules()
        {
            var validationRule = new ValidationRule
            {
                AttributeName = AttributeNames.CropType,
                MandatoryType = MandatoryType.PointAndPolygon,
                MissingErrorMessage = "CropType (CT) attribute is missing in dataset.",
                InvalidValueErrorMessage = "CropType (CT) attribute has invalid value(s)",
                ValueChecks = new[] {ValueCheckType.Integer}
            };

            var values = new[]
            {
                0, 1000, 1100, 1110, 1120, 1200, 1300, 1400, 1500, 1510, 1520, 1600, 1610, 1620, 1700, 1800, 1900, 1910,
                1920, 2000, 2100, 2110, 2120, 2130, 2140, 2150, 2160, 2170, 2190, 2200, 2210, 2220, 2230, 2240, 2250,
                2260, 2290, 2300, 2310, 2320, 2330, 2340, 2350, 2390, 2400, 2900, 3000, 3100, 3110, 3120, 3130, 3140,
                3150, 3160, 3170, 3190, 3200, 3210, 3220, 3230, 3240, 3290, 3300, 3400, 3410, 3420, 3430, 3440, 3450,
                3460, 3490, 3500, 3510, 3520, 3530, 3540, 3550, 3560, 3590, 3600, 3610, 3620, 3630, 3640, 3650, 3660,
                3690, 3900, 4000, 4100, 4200, 4300, 4310, 4320, 4330, 4340, 4350, 4351, 4352, 4360, 4370, 4380, 4390,
                4400, 4410, 4420, 4430, 4490, 5000, 5100, 5200, 5300, 5400, 5900, 6000, 6100, 6110, 6120, 6130, 6140,
                6190, 6200, 6211, 6212, 6219, 6221, 6222, 6223, 6224, 6225, 6226, 6229, 7000, 7100, 7200, 7300, 7400,
                7500, 7600, 7700, 7800, 7900, 7910, 7920, 8000, 8100, 8200, 8300, 8900, 9000, 9100, 9110, 9120, 9200,
                9210, 9211, 9212, 9213, 9219, 9220, 9300, 9310, 9320, 9400, 9500, 9510, 9520, 9600, 9900, 9910, 9920,
                9998
            };

            var validValues = values.Select(x => new RuleValidValue {Name = "CT", Value = x.ToString()}).ToList();
            validationRule.RuleValidValues = validValues;
            return validationRule;
        }

        private static ValidationRule GetIrrRules()
        {
            var validationRule = new ValidationRule
            {
                AttributeName = AttributeNames.Irrigation,
                MandatoryType = MandatoryType.PointAndPolygon,
                MissingErrorMessage = "IrrigationType (IRR) attribute is missing in dataset.",
                InvalidValueErrorMessage = "IrrigationType (IRR) attribute has invalid value(s)",
                ValueChecks = new[] {ValueCheckType.Integer}
            };

            var values = new[] {0, 100, 200, 210, 213, 214, 215, 220, 223, 224, 225};
            var validValues = values.Select(x => new RuleValidValue {Name = "IRR", Value = x.ToString()}).ToList();
            validationRule.RuleValidValues = validValues;
            return validationRule;
        }

        private static ValidationRule GetLcRules()
        {
            var validationRule = new ValidationRule
            {
                AttributeName = AttributeNames.LandCover,
                MandatoryType = MandatoryType.PointAndPolygon,
                MissingErrorMessage = "LandCover (LC) attribute is missing in dataset.",
                InvalidValueErrorMessage = "LandCover (LC) attribute has invalid value(s)",
                ValueChecks = new[] {ValueCheckType.Integer}
            };

            var lcValues = new[] {0, 10, 11, 12, 13, 20, 30, 40, 41, 42, 50, 60, 70, 80, 99};
            var validValues = lcValues.Select(x => new RuleValidValue {Name = "LC", Value = x.ToString()}).ToList();
            validationRule.RuleValidValues = validValues;
            return validationRule;
        }

        private static ValidationRule GetOptionalImageryTimeRules()
        {
            var validationRule = new ValidationRule
            {
                AttributeName = AttributeNames.ImageryTime,
                MandatoryType = MandatoryType.None,
                MissingErrorMessage = "Imagery Time (imtime) optional attribute is missing in dataset.",
                InvalidValueErrorMessage =
                    "Optional Imagery Time (imtime) attribute has invalid value(s), Date should be in YYYY-MM-DD format as a string data.",
                ValueChecks = new[] {ValueCheckType.Date}
            };

            return validationRule;
        }

        private static ValidationRule GetOptionalNumberOfValidationsRules()
        {
            var validationRule = new ValidationRule
            {
                AttributeName = AttributeNames.NumberOfValidations,
                MandatoryType = MandatoryType.None,
                MissingErrorMessage = "number Of Validations (numberval) optional attribute is missing in dataset.",
                InvalidValueErrorMessage =
                    "Optional number Of Validations (numberval) attribute supports non negative whole number, uploaded dataset has invalid value(s)",
                ValueChecks = new[] {ValueCheckType.Integer}
            };

            return validationRule;
        }

        private static ValidationRule GetOptionalAgreementOfObsRules()
        {
            var validationRule = new ValidationRule
            {
                AttributeName = AttributeNames.AgreementOfObservations,
                MandatoryType = MandatoryType.None,
                MissingErrorMessage = "agreement Of Observations (agreement) optional attribute is missing in dataset.",
                InvalidValueErrorMessage =
                    "Optional agreement Of Observations (agreement) attribute supports non negative whole number, uploaded dataset has invalid value(s)",
                ValueChecks = new[] {ValueCheckType.Integer}
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
                ValueChecks = new[] {ValueCheckType.Integer}
            };

            return validationRule;
        }

        private static ValidationRule GetOptionalTypeOfValidatorRules()
        {
            var validationRule = new ValidationRule
            {
                AttributeName = AttributeNames.TypeOfValidator,
                MandatoryType = MandatoryType.None,
                MissingErrorMessage = "Type Of Validator (typeval) optional attribute is missing in dataset.",
                InvalidValueErrorMessage =
                    "Optional Type Of Validator (typeval) attribute valid values are [Expert,NonExpert,Both] case sensitive. Uploaded dataset has invalid value(s)",
                ValueChecks = new[] {ValueCheckType.Text}
            };

            var validValues = new[] {"Expert", "NonExpert", "Both"};
            validationRule.RuleValidValues = validValues
                .Select(x => new RuleValidValue {Name = AttributeNames.TypeOfValidator, Value = x}).ToList();

            return validationRule;
        }

        private static ValidationRule GetOptionalSupportingRadiusRules()
        {
            var validationRule = new ValidationRule
            {
                AttributeName = AttributeNames.SupportingRadius,
                MandatoryType = MandatoryType.None,
                MissingErrorMessage = "supporting Radius (supradius) optional attribute is missing in dataset.",
                InvalidValueErrorMessage =
                    "Optional supporting Radius (supradius) attribute supports non negative decimal numbers. Currently it has invalid value(s)",
                ValueChecks = new[] {ValueCheckType.Decimal}
            };

            return validationRule;
        }
    }
}