using System;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Dtos;

namespace IIASA.WorldCereal.Rdm.Core
{
    public interface IItemsCodeStatsHelper
    {
        Task<CodeStats> GetCodeStats(string collectionId, Guid tenantId);
    }
}