using System;
using IIASA.WorldCereal.Rdm.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace IIASA.WorldCereal.Rdm.Entity
{
    public class DatasetEvent : CreationAuditedEntity<int>, IMultiTenant
    {
        public Guid? TenantId { get; set; }
        public EventType Type { get; set; }
        public string[] Comments { get; set; }
    }
}