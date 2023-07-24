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
        [Required] public DateTime ValidityTime { get; set; } //YYYY-MM-DD
        public double Area { get; set; }
        public SplitType Split { get; set; }   
        public int Lc { get; set; }
        public int Ct { get; set; }
        public int Irr { get; set; }
        public int UserConf { get; set; }
        public DateTime? ImageryTime { get; set; }
        public int? NumberOfValidations { get; set; }
        public ValidatorType? TypeOfValidator { get; set; }
        public int? AgreementOfObservations { get; set; }
        public int? DisAgreementOfObservations { get; set; }
    }
}