using System;
using IIASA.WorldCereal.Rdm.Enums;
using Volo.Abp.Application.Dtos;

namespace IIASA.WorldCereal.Rdm.Dtos
{
    public class CollectionSummaryDto : AuditedEntityDto<Guid>
    {
        public string CollectionId { get; set; } // tenant name
        public int[] LandCovers { get; set; }
        public int[] CropTypes { get; set; }
        public int[] IrrTypes { get; set; }
        public CollectionType Type { get; set; }
        public string Title { get; set; }
        public TypeOfObservationMethod TypeOfObservationMethod { get; set; }
        public long FeatureCount { get; set; }
        public ExtentDto Extent { get; set; }
        public StoreType StoreType { get; set; }
        public AccessType AccessType { get; set; }
        public string AdditionalData { get; set; }
        public string[] Crs { get; set; } = {"http://www.opengis.net/def/crs/EPSG/0/4326"};
    }
}