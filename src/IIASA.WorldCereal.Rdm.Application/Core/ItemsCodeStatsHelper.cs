using System;
using System.Linq;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Dtos;
using IIASA.WorldCereal.Rdm.Entity;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Volo.Abp.Domain.Repositories;

namespace IIASA.WorldCereal.Rdm.Core
{
    public class ItemsCodeStatsHelper : IItemsCodeStatsHelper
    {
        private readonly IRepository<MetadataItem, int> _metadataItemsRepository;
        private readonly IRepository<ItemEntity, long> _itemRepository;
        private const string CodeStats = "codeStats";

        public ItemsCodeStatsHelper(IRepository<MetadataItem, int> metadataItemsRepository,
            IRepository<ItemEntity, long> itemRepository)
        {
            _metadataItemsRepository = metadataItemsRepository;
            _itemRepository = itemRepository;
        }

        public async Task<CodeStats> GetCodeStats(string collectionId, Guid tenantId)
        {
            if (_metadataItemsRepository.Any(x => x.Name == CodeStats))
            {
                var metadataItem = await _metadataItemsRepository.FindAsync(x => x.Name == CodeStats);
                var stats = JsonConvert.DeserializeObject<CodeStats>(metadataItem.Value);
                if (stats != null)
                {
                    stats.CtStats = stats.CtStats.OrderByDescending(x => x.Count).ToArray();
                    stats.LcStats = stats.LcStats.OrderByDescending(x => x.Count).ToArray();
                    stats.IrrStats = stats.IrrStats.OrderByDescending(x => x.Count).ToArray();
                    return stats;
                }
            }

            var codeStats = new CodeStats
            {
                LcStats = await _itemRepository.GroupBy(x => x.Lc)
                    .Select(x => new StatsItem {Code = x.Key, Count = x.Count()}).OrderByDescending(x => x.Count)
                    .ToArrayAsync(),
                CtStats = await _itemRepository.GroupBy(x => x.Ct)
                    .Select(x => new StatsItem {Code = x.Key, Count = x.Count()}).OrderByDescending(x => x.Count)
                    .ToArrayAsync(),
                IrrStats = await _itemRepository.GroupBy(x => x.Irr)
                    .Select(x => new StatsItem {Code = x.Key, Count = x.Count()}).OrderByDescending(x => x.Count)
                    .ToArrayAsync()
            };

            await _metadataItemsRepository.InsertAsync(new MetadataItem
                {Name = CodeStats, TenantId = tenantId, Value = JsonConvert.SerializeObject(codeStats)});

            return codeStats;
        }
    }
}