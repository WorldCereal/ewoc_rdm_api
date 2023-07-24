using System;
using IIASA.WorldCereal.Rdm.Enums;
using Volo.Abp.Application.Dtos;

namespace IIASA.WorldCereal.Rdm.Dtos
{
    public class UserDatasetViewModel : AuditedEntityDto<Guid>
    {
        public string Title { get; set; }
        public TypeOfObservationMethod TypeOfObservationMethod { get; set; }
        public double ConfidenceLandCover { get; set; }
        public double ConfidenceCropType { get; set; }
        public double ConfidenceIrrigationType { get; set; }
        public string CollectionId { get; set; }
        public UserDatasetState State { get; set; }
        public string[] Errors { get; set; }
    }

    public class DatasetEventViewModel : CreationAuditedEntityDto<int>
    {
        public EventType Type { get; set; }
        public string[] Comments { get; set; }
        public bool CanSubmit { get; set; }
    }

    public class DownloadDatasetRequest : PagedResultRequestDto
    {
        public DownloadDatasetRequest()
        {
            MaxResultCount = 1000;
        }
    }
}