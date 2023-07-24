using System;
using IIASA.WorldCereal.Rdm.Enums;
using Volo.Abp.Domain.Entities.Auditing;

namespace IIASA.WorldCereal.Rdm.Entity
{
    public class UserDataset : AuditedEntity<Guid>
    {
        public string Title { get; set; }
        public string CollectionId { get; set; }
        public TypeOfObservationMethod TypeOfObservationMethod { get; set; }
        public double ConfidenceLandCover { get; set; }
        public double ConfidenceCropType { get; set; }
        public double ConfidenceIrrigationType { get; set; }
        public UserDatasetState State { get; set; }
        public string[] Errors { get; set; }
        public Guid MasterCollectionStoreInfoId { get; set; }
        public  string UserId { get; set; }
    }
}