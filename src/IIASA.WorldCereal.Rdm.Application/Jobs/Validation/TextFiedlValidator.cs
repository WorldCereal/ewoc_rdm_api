using System.Linq;
using IIASA.WorldCereal.Rdm.Enums;

namespace IIASA.WorldCereal.Rdm.Jobs.Validation
{
    public class TextFiedlValidator:IFieldValidator
    {
        private string[] _validValues;
        public ValueCheckType ValueCheckType { get; set; } = ValueCheckType.Text;
        public bool IsValid(object value)
        {
            var stringValue = value.ToString();
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return false;
            }

            return _validValues.Length > 0 ? _validValues.Contains(stringValue) : true;
        }

        public string AttributeName { get; set; }
        public void SetValidValues(string[] values)
        {
            _validValues = values;
        }
    }
}