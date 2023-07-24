using System;
using System.Linq;
using IIASA.WorldCereal.Rdm.Enums;

namespace IIASA.WorldCereal.Rdm.Jobs.Validation
{
    public class IntegerFieldValidator : IFieldValidator
    {
        private int[] _validValues = new int[0];
        public ValueCheckType ValueCheckType { get; set; } = ValueCheckType.Integer;

        public bool IsValid(object value)
        {
            try
            {
                var intValue = int.Parse(value.ToString());
                return _validValues.Count() > 0 ? _validValues.Contains(intValue) : intValue>=0;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }

        public string AttributeName { get; set; }

        public void SetValidValues(string[] values)
        {
            _validValues = values.Select(x => int.Parse(x)).ToArray();
        }
    }
}
