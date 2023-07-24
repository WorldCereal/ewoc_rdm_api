using System;
using System.Collections.Generic;

namespace IIASA.WorldCereal.Rdm.Dtos
{
    public class Temporal
    {
        public IEnumerable<IEnumerable<DateTime?>> Interval { get; set; }
        public string Trs { get; set; } = "http://www.opengis.net/def/uom/ISO-8601/0/Gregorian";
    }
}