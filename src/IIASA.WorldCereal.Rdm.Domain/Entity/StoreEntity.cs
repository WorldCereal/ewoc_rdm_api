using System;
using IIASA.WorldCereal.Rdm.Enums;
using Volo.Abp.Domain.Entities.Auditing;

namespace IIASA.WorldCereal.Rdm.Entity
{
    public class StoreEntity : AuditedEntity<Guid>
    {
        public string ConnectionString { get; set; }

        public StoreType StoreType { get; set; }

        public int Count { get; set; }

        public string Name { get; set; } // readable name for store
    }
}