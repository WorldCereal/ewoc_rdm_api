using System;
using IIASA.WorldCereal.Rdm.Enums;
using Volo.Abp.Application.Dtos;

namespace IIASA.WorldCereal.Rdm.Dtos
{
    public class StoreDto : AuditedEntityDto<Guid>
    {
        public string ConnectionString { get; set; }

        public StoreType StoreType { get; set; }

        public string Name { get; set; }

        public int Count { get; set; }
    }
}