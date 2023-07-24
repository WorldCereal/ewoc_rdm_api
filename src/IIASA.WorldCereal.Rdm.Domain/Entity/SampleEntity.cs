using System;
using System.ComponentModel.DataAnnotations;
using IIASA.WorldCereal.Rdm.Enums;
using NetTopologySuite.Geometries;
using Volo.Abp.Domain.Entities.Auditing;

namespace IIASA.WorldCereal.Rdm.Entity
{
    public class SampleEntity : AuditedEntity<long>
    {
        [Required] public double Version { get; set; } = 1.0; // version Id 
        [Required] public Geometry Geometry { set; get; }
        [Required] public SplitType Split { get; set; }
        [Required] public DateTime ValidityStartTime { get; set; }
        [Required] public DateTime ValidityEndTime { get; set; }
    }
}