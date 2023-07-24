using System;
using System.ComponentModel.DataAnnotations;
using IIASA.WorldCereal.Rdm.Enums;
using NetTopologySuite.Geometries;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace IIASA.WorldCereal.Rdm.Entity
{
    // need to decide based on metadata doc
    // goes in CollectionId tenant DB
    public class CollectionMetadataEntity : AuditedEntity<Guid>, IMultiTenant
    {
        public Guid? TenantId { get; set; }
        [Required] public string CollectionId { get; set; } // tenant name
        public StoreType StoreType { get; set; }
        public CollectionType Type { get; set; }
        public AccessType AccessType { get; set; }
        public string Title { get; set; }
        public TypeOfObservationMethod TypeOfObservationMethod { get; set; }
        public string Description { get; set; }
        public long FeatureCount { get; set; }
        public double ConfidenceLandCover { get; set; }
        public double ConfidenceCropType { get; set; }
        public double ConfidenceIrrigationType{ get; set; }
        public int[] LandCovers { get; set; }
        public int[] CropTypes { get; set; }
        public int[] IrrTypes { get; set; }
        [Required] public Geometry Extent { get; set; }
        [Required] public DateTime FirstDateOfValidityTime { get; set; }
        [Required] public DateTime LastDateOfValidityTime { get; set; }
        public string AdditionalData { get; set; } // to store raster patches in s3 and store base url here
    }
}
