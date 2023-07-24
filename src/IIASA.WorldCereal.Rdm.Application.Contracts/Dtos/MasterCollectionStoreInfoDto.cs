using System;
using System.Collections.Generic;
using IIASA.WorldCereal.Rdm.Enums;
using Volo.Abp.Application.Dtos;

namespace IIASA.WorldCereal.Rdm.Dtos
{
    public class MasterCollectionStoreInfoDto: AuditedEntityDto<Guid>, ICollectionProps
    {
        public string CollectionId { get; set; } // tenant name
        public CollectionType Type { get; set; }
        public int[] LandCovers { get; set; }
        public int[] CropTypes { get; set; }
        public int[] IrrTypes { get; set; }
        public IEnumerable<CoordinateDto> BoundingBoxPoints { get; set; }
        /// <summary>
        /// The optional FirstDateOfValidityTime eg - 2019-06-30T00:00:00Z (yyyy-MM-ddTHH:mm:ssZ)
        /// </summary>
        public DateTime FirstDateOfValidityTime { get; set; }
        /// <summary>
        /// The optional LastDateOfValidityTime eg - 2019-06-30T00:00:00Z (yyyy-MM-ddTHH:mm:ssZ)
        /// </summary>
        public DateTime LastDateOfValidityTime { get; set; }
        public string AdditionalData { get; set; } // to store raster patches in s3 and store base url here
        public StoreType StoreType { get; set; }

        public AccessType AccessType { get; set; }
        public string Title { get; set; }
        public long FeatureCount { get; set; }
    }

    public class ConsortiumCollectionDto : AuditedEntityDto<int>
    {
        public string DownloadZipUrl { get; set; }
        
        public bool Successful { get; set; }
        
        public string Errors{ get; set; }
        
        public bool NeedsUpdate{ get; set; }
        
        public bool OverwriteFeatures{ get; set; }
        
        public bool OverwriteOtherProps{ get; set; }
    }
}