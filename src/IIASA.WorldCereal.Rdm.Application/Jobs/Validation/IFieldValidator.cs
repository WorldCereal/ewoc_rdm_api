using IIASA.WorldCereal.Rdm.Enums;

namespace IIASA.WorldCereal.Rdm.Jobs.Validation
{
    public interface IFieldValidator
    {
        ValueCheckType ValueCheckType { get; set; }

        bool IsValid(object value);

        public string AttributeName { get; set; }

        void SetValidValues(string[] values);
    }
}
