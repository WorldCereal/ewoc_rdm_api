using System.Collections;
using IIASA.WorldCereal.Rdm.Enums;

namespace IIASA.WorldCereal.Rdm.Dtos
{
    public class MetadataItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public MetadataItemType Type { get; set; } = MetadataItemType.text;
    }

    public class CodeStats
    {
        public StatsItem[] LcStats { get; set; }
        public StatsItem[] CtStats { get; set; }
        public StatsItem[] IrrStats { get; set; }
    }

    public class StatsItem
    {
        public int Code { get; set; }

        public int Count { get; set; }
    }
}