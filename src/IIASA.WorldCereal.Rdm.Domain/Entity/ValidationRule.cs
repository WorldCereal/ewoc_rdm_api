using System.Collections.Generic;
using IIASA.WorldCereal.Rdm.Enums;
using Volo.Abp.Domain.Entities.Auditing;

namespace IIASA.WorldCereal.Rdm.Entity
{
    public class ValidationRule : AuditedEntity<int>
    {
        public string AttributeName { get; set; } //case sensitiveName

        public ValueCheckType[] ValueChecks { get; set; }

        public MandatoryType MandatoryType { get; set; }

        public string MissingErrorMessage { get; set; }

        public string InvalidValueErrorMessage { get; set; }

        public List<RuleValidValue> RuleValidValues { get; set; } = new List<RuleValidValue>();
    }
}