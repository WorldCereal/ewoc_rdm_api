using System;
using System.Linq;
using IIASA.WorldCereal.Rdm.Enums;

namespace IIASA.WorldCereal.Rdm.Jobs.Validation;

public class BigIntFieldValidator : IFieldValidator
{
    private long[] _validValues = new long[0];
    public ValueCheckType ValueCheckType { get; set; } = ValueCheckType.BigInteger;

    public bool IsValid(object value)
    {
        try
        {
            var longValue = long.Parse(value.ToString());
            return _validValues.Count() > 0 ? _validValues.Contains(longValue) : longValue>=0;
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
        _validValues = values.Select(x => long.Parse(x)).ToArray();
    }
}