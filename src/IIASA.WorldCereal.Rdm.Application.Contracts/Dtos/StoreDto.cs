using System;
using IIASA.WorldCereal.Rdm.Enums;
using Volo.Abp.Application.Dtos;

namespace IIASA.WorldCereal.Rdm.Dtos
{
    public class StoreDto : EntityDto<Guid>
    {
        public string ConnectionString { get; set; }

        public StoreType StoreType { get; set; }

        public string Name { get; set; }

        public int Count { get; set; }
    }

    public class EoTypeDto : EntityDto<int>
    {
        public string Name { get; set; }
        public long Value { get; set; }
        public CodeType CodeType { get; set; }
    }

    public class CollectionStats
    {
        public int TotalCollections { get; set; }
        public long FeaturesTotalCount { get; set; }
    }
}