using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using IIASA.WorldCereal.Rdm.Enums;

namespace IIASA.WorldCereal.Rdm.Dtos
{
    public class AddMasterCollectionStoreInfoDto: ICollectionProps
    {
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
        [Required] public IEnumerable<CoordinateDto> BoundingBoxPoints { get; set; }
        /// <summary>
        /// FirstDateOfValidityTime eg - 2019-06-30T00:00:00Z (yyyy-MM-ddTHH:mm:ssZ)
        /// </summary>
        [Required] public DateTime FirstDateOfValidityTime { get; set; }
        /// <summary>
        /// LastDateOfValidityTime eg - 2019-06-30T00:00:00Z (yyyy-MM-ddTHH:mm:ssZ)
        /// </summary>
        [Required] public DateTime LastDateOfValidityTime { get; set; }
        public string AdditionalData { get; set; }
    }
}