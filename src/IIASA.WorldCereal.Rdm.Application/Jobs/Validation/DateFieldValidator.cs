using System;
using System.Globalization;
using IIASA.WorldCereal.Rdm.Enums;

namespace IIASA.WorldCereal.Rdm.Jobs.Validation
{
    public class DateFieldValidator : IFieldValidator
    {
        private readonly DateTime _validStartDate = new(2017, 1, 1);
        private readonly DateTime _currentDate = DateTime.UtcNow;
        public ValueCheckType ValueCheckType { get; set; } = ValueCheckType.Date;

        public bool IsValid(object value)
        {
            try
            {
                if (value is DateTime date)
                {
                    return IsValidDate(date);
                }

                if (value is long longObj)
                {
                    var dateTimeOffset = DateTimeOffset.FromFileTime(longObj);
                    return IsValidDate(dateTimeOffset.DateTime);
                }

                char[] separators = ['T', ' '];
                var stringValue = value.ToString();
                var parsedValue =
                    DateTime.ParseExact(stringValue.Split(separators)[0], "yyyy-M-d", CultureInfo.InvariantCulture);
                return IsValidDate(parsedValue);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public string AttributeName { get; set; }

        public void SetValidValues(string[] values)
        {
            //not used
        }

        private bool IsValidDate(DateTime date)
        {
            return date >= _validStartDate && date < _currentDate;
        }
    }
}