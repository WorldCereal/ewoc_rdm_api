using System;
using IIASA.WorldCereal.Rdm.Enums;
using Volo.Abp.Application.Dtos;

namespace IIASA.WorldCereal.Rdm.Dtos
{
    // need to add all the metadata based on the document in teams
    public class CollectionMetadataDto : AuditedEntityDto<Guid>
    {
        public string CollectionId { get; set; } // tenant name
        public string Title { get; set; }
        public long FeatureCount { get; set; }
        public CollectionType Type { get; set; }
        public AccessType AccessType { get; set; }
        public TypeOfObservationMethod TypeOfObservationMethod { get; set; }
        public int ConfidenceLandCover { get; set; }
        public int ConfidenceCropType { get; set; }
        public int ConfidenceIrrigationType{ get; set; }
        public long[] EwocCodes { get; set; }
        public int[] IrrTypes { get; set; }
        public ExtentDto Extent { get; set; }
        public string AdditionalData { get; set; }  // for raster store details
        public string[] Crs { get; set; } = {"http://www.opengis.net/def/crs/EPSG/0/4326"};
    }
}