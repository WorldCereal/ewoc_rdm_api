using Volo.Abp.Domain.Entities.Auditing;

namespace IIASA.WorldCereal.Rdm.Entity
{
    public class RuleValidValue : AuditedEntity<int>
    {
        public string Name { get; set; }

        public string Value { get; set; }
    }
}