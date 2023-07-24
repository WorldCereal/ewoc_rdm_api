using System;
using IIASA.WorldCereal.Rdm.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace IIASA.WorldCereal.Rdm.Entity
{
    public class MetadataItem : AuditedEntity<int>, IMultiTenant
    {
        public Guid? TenantId { get; set; }
        
        public string Name { get; set; }

        public string Value { get; set; }

        public MetadataItemType Type { get; set; } = MetadataItemType.text;
    }
}