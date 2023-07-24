using System.ComponentModel.DataAnnotations;
using IIASA.WorldCereal.Rdm.Enums;

namespace IIASA.WorldCereal.Rdm.Dtos
{
    public class CreateOrUpdateUserDataset
    {
        [Required] public string Title { get; set; }
        [Required] public TypeOfObservationMethod TypeOfObservationMethod { get; set; }
        [Required] public string CollectionId { get; set; }
        [Required] public double ConfidenceLandCover { get; set; }
        [Required] public double ConfidenceCropType { get; set; }
        [Required] public double ConfidenceIrrigationType { get; set; }
    }
}