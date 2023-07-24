using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace IIASA.WorldCereal.Rdm.Interfaces
{
    public interface IUserDatasetService : IApplicationService
    {
        Task<IEnumerable<UserDatasetViewModel>> GetUserDatasets(PagedResultRequestDto pagedResultRequestDto);
        Task<UserDatasetViewModel> GetUserDataset(Guid id);
    }
}