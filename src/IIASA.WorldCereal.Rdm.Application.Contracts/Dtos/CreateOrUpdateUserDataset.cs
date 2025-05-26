using System.ComponentModel.DataAnnotations;
using IIASA.WorldCereal.Rdm.Enums;

namespace IIASA.WorldCereal.Rdm.Dtos
{
    public class CreateOrUpdateUserDataset
    {
        [Required] public string Title { get; set; }
        [Required] public string CollectionId { get; set; }
    }

    public class CreateOrUpdateUserDatasetColumnNames
    {
        [Required] public string EwocColumnName { get; set; }
        [Required] public string ValTimeColumnName { get; set; }
        [Required] public bool IsManualValidityDate { get; set; } = false;
        public string DateFormat { get; set; } = string.Empty;
        public string? IrrColumnName { get; set; }
    }
}