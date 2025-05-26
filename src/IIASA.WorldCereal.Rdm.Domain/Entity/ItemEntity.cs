using System;
using System.ComponentModel.DataAnnotations;
using IIASA.WorldCereal.Rdm.Enums;
using NetTopologySuite.Geometries;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace IIASA.WorldCereal.Rdm.Entity
{
    public class ItemEntity : AuditedEntity<long>, IMultiTenant
    {
        public ItemEntity()
        {
            CreationTime = DateTime.UtcNow;
        }
        
        public Guid? TenantId { get; set; } 
        [Required] public string CollectionId { get; set; } // tenant name
        [Required] public string SampleId { get; set; } // item Id
        [Required] public Geometry Geometry { set; get; }
        [Required] public long EwocCode { get; set; }
        [Required] public int IrrigationStatus { get; set; }
        [Required] public DateTime ValidityTime { get; set; } //YYYY-MM-DD
        public int Extract { get; set; }
        public string H3Index { get; set; }
        public int LcQualityScore { get; set; }
        public int CtQualityScore { get; set; }
        public int QualityScore { get; set; }
        public DateTime? ImageryDate { get; set; }
        public int? NumberOfValidations { get; set; }
        public ValidatorType? TypeOfValidator { get; set; }
        public int? AgreementOfObservations { get; set; }
        public int? DisAgreementOfObservations { get; set; }
    }
}