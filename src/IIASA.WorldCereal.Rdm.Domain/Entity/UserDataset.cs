using System;
using IIASA.WorldCereal.Rdm.Enums;
using Volo.Abp.Domain.Entities.Auditing;

namespace IIASA.WorldCereal.Rdm.Entity
{
    public class UserDataset : AuditedEntity<Guid>
    {
        public string Title { get; set; } = string.Empty;
        public string CollectionId { get; set; } = string.Empty;

        public string[] Columns { get; set; } = [];

        public string EwocCodeCol { get; set; } = string.Empty;

        public string ValDateCol { get; set; } = string.Empty;

        public string IrrCol { get; set; } = string.Empty;
        public UserDatasetState State { get; set; }
        public string[] Errors { get; set; } = [];
        public Guid MasterCollectionStoreInfoId { get; set; }
        public string UserId { get; set; } = string.Empty;

        public string DatasetExtractedPath { get; set; }
    }
}