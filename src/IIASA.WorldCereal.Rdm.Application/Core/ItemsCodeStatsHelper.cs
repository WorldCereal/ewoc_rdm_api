using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Dtos;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.ExcelOps;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NPOI.SS.Formula.Functions;
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
            if ((await _metadataItemsRepository.GetQueryableAsync()).Any(x => x.Name == CodeStats))
            {
                var metadataItem = await (await _metadataItemsRepository.GetQueryableAsync())
                    .OrderByDescending(o => o.CreationTime).FirstOrDefaultAsync(x => x.Name == CodeStats);
                if (metadataItem != null)
                {
                    var stats = JsonConvert.DeserializeObject<CodeStats>(metadataItem.Value);
                    if (stats != null)
                    {
                        return stats;
                    }
                }
            }

            var codeStats = new CodeStats
            {
                EwocStats = await (await _itemRepository.GetQueryableAsync()).GroupBy(x => x.EwocCode)
                    .Select(x => new StatsItem { Code = x.Key, Count = x.Count() }).OrderByDescending(x => x.Count)
                    .ToArrayAsync(),
                IrrStats = await (await _itemRepository.GetQueryableAsync()).GroupBy(x => x.IrrigationStatus)
                    .Select(x => new StatsItem { Code = x.Key, Count = x.Count() }).OrderByDescending(x => x.Count)
                    .ToArrayAsync()
            };

            var lcStats = new Dictionary<long, int>();
            foreach (var item in codeStats.EwocStats)
            {
                var code = item.Code == 0 ? 0 : long.Parse(item.Code.ToString()[..2]);
                if (lcStats.ContainsKey(code))
                {
                    lcStats[code] += item.Count;
                }
                else
                {
                    lcStats.Add(code, item.Count);
                }
            }

            codeStats.LcStats = lcStats.Select(x => new StatsItem() { Code = x.Key, Count = x.Value }).ToArray();

            await _metadataItemsRepository.InsertAsync(new MetadataItem
                { Name = CodeStats, TenantId = tenantId, Value = JsonConvert.SerializeObject(codeStats) });

            return codeStats;
        }

        public static int GetValue(List<ExcelMetadata> metadataItems, string marker)
        {
            if (metadataItems.All(x => x.Name != marker))
            {
                return 0;
            }

            if (int.TryParse(metadataItems.First(x => x.Name == marker).Value, out var value))
            {
                return value;
            }

            if (double.TryParse(metadataItems.First(x => x.Name == marker).Value, out var dValue))
            {
                return (int)dValue;
            }

            return 0;
        }

        public static MetadataItem GetCodeStats(IList<ItemEntity> entities)
        {
            var codeStats = new CodeStats
            {
                EwocStats = entities.GroupBy(x => x.EwocCode)
                    .Select(x => new StatsItem { Code = x.Key, Count = x.Count() }).OrderByDescending(x => x.Count)
                    .ToArray(),
                IrrStats = entities.GroupBy(x => x.IrrigationStatus)
                    .Select(x => new StatsItem { Code = x.Key, Count = x.Count() }).OrderByDescending(x => x.Count)
                    .ToArray()
            };

            var lcStats = new Dictionary<long, int>();
            foreach (var item in codeStats.EwocStats)
            {
                var substring = item.Code.ToString().Substring(0, 2);
                var code = item.Code == 0 ? 0 : long.Parse(substring);
                if (lcStats.ContainsKey(code))
                {
                    lcStats[code] += item.Count;
                }
                else
                {
                    lcStats.Add(code, item.Count);
                }
            }

            codeStats.LcStats = lcStats.Select(x => new StatsItem() { Code = x.Key, Count = x.Value }).ToArray();

            return new MetadataItem
                { Name = CodeStats, Value = JsonConvert.SerializeObject(codeStats) };
        }
    }
}