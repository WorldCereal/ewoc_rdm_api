using System;
using System.Linq;
using IIASA.WorldCereal.Rdm.Enums;

namespace IIASA.WorldCereal.Rdm.Jobs.Validation
{
    public class DecimalFieldValidator : IFieldValidator    
    {
        private double[] _validValues = new double[0];
        public ValueCheckType ValueCheckType { get; set; } = ValueCheckType.Decimal;

        public bool IsValid(object value)
        {
            try
            {
                var parsedValue = double.Parse(value.ToString());
                return _validValues.Count() > 0 ? _validValues.Contains(parsedValue) : parsedValue >= 0.0;
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
            _validValues = values.Select(x => double.Parse(x)).ToArray();
        }
    }
}