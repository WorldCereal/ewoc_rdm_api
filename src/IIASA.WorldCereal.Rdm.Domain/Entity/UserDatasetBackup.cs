using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace IIASA.WorldCereal.Rdm.Entity
{
    public class UserDatasetBackup : AuditedEntity<Guid>
    {
        public Guid UserDatasetId { get; set; }
        public string CollectionId { get; set; }
        public string ZipFilePath { get; set; }
    }
}