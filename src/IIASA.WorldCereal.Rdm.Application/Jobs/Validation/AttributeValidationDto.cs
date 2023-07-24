using System.Collections.Generic;
using IIASA.WorldCereal.Rdm.Entity;

namespace IIASA.WorldCereal.Rdm.Jobs.Validation
{
    public class AttributeValidationDto
    {
        public ValidationRule ValidationRule { get; set; }

        public int Index { get; set; }

        public List<string> ErrorValues { get; set; } = new();

        public IFieldValidator FieldValidator { get; set; }
    }
}
