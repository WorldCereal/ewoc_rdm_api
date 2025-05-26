using IIASA.WorldCereal.Rdm.Enums;
using Volo.Abp.Domain.Entities.Auditing;

namespace IIASA.WorldCereal.Rdm.Entity;

public class EoType : AuditedEntity<int>
{
    public string Name { get; set; }
    public long Value { get; set; }
    public CodeType CodeType { get; set; }
}