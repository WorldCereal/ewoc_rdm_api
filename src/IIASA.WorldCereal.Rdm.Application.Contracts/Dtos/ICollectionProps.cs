using System;
using System.Collections.Generic;
using IIASA.WorldCereal.Rdm.Enums;

namespace IIASA.WorldCereal.Rdm.Dtos
{
    public interface ICollectionProps
    {
        public string CollectionId { get; set; } // tenant name
        public CollectionType Type { get; set; }
        public int[] LandCovers { get; set; }
        public int[] CropTypes { get; set; }
        public int[] IrrTypes { get; set; }
        public IEnumerable<CoordinateDto> BoundingBoxPoints { get; set; }
        public DateTime FirstDateOfValidityTime { get; set; }
        public DateTime LastDateOfValidityTime { get; set; }
        public string AdditionalData { get; set; }
    }
}