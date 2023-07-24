using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Dtos;
using IIASA.WorldCereal.Rdm.Entity;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace IIASA.WorldCereal.Rdm.Services
{
    [RemoteService(Name = "OGC")]
    [ApiExplorerSettings(GroupName = "OGC")]
    public class StoreService : RdmAppService
    {
        private readonly IRepository<StoreEntity, Guid> _storeRepository;
        private readonly IAddCollectionHelper _addCollectionHelper;
        private readonly IRepository<ConsortiumCollection> _consortiumCollectionsRepository;
        private readonly IEwocUser _ewocUser;

        public StoreService(IRepository<StoreEntity, Guid> storeRepository, IAddCollectionHelper addCollectionHelper,
            IRepository<ConsortiumCollection> consortiumCollectionsRepository,
            IEwocUser ewocUser)
        {
            _storeRepository = storeRepository;
            _addCollectionHelper = addCollectionHelper;
            _consortiumCollectionsRepository = consortiumCollectionsRepository;
            _ewocUser = ewocUser;
        }

        [HttpGet("store")]
        public async Task<IEnumerable<StoreDto>> Get()
        {
            if (_ewocUser.IsAdmin == false)
            {
                throw new UnauthorizedAccessException("Admin Auth required.");
            }

            var stores = await _storeRepository.GetListAsync();
            return stores.Select(x => ObjectMapper.Map<StoreEntity, StoreDto>(x));
        }

        [HttpGet("user/isadmin")]
        public bool GetIsAdmin()
        {
            return _ewocUser.IsAdmin;
        }

        [HttpPost("store")]
        public async Task<StoreDto> Create(AddStoreDto storeDto)
        {
            if (_ewocUser.IsAdmin == false)
            {
                throw new UnauthorizedAccessException("Admin Auth required.");
            }

            var storeToAdd = ObjectMapper.Map<AddStoreDto, StoreEntity>(storeDto);
            var newStore = await _storeRepository.InsertAsync(storeToAdd);
            return ObjectMapper.Map<StoreEntity, StoreDto>(newStore);
        }

        [HttpGet("store/ref/collection")]
        public async Task<IEnumerable<ConsortiumCollectionDto>> GetCollections()
        {
            if (_ewocUser.IsAdmin == false)
            {
                throw new UnauthorizedAccessException("Admin Auth required.");
            }

            var stores = await _consortiumCollectionsRepository.GetListAsync();
            return stores.Select(x => ObjectMapper.Map<ConsortiumCollection, ConsortiumCollectionDto>(x));
        }

        [HttpPost("store/ref/collection")]
        public async Task<IActionResult> AddCollection(string downloadUrl, bool update)
        {
            if (_ewocUser.IsAdmin == false)
            {
                throw new UnauthorizedAccessException("Admin Auth required.");
            }

            var newStore = await _consortiumCollectionsRepository.InsertAsync(new ConsortiumCollection
                {DownloadZipUrl = downloadUrl, NeedsUpdate = update});
            return new OkObjectResult(new
            {
                Result = ObjectMapper.Map<ConsortiumCollection, ConsortiumCollectionDto>(newStore),
                Message = "Added collection to table. Next you need to run the ref db update job in k8s."
            });
        }

        [HttpPut("store/ref/collection/{id:int}")]
        public async Task UpdateCollection(int id, bool update, bool overwriteFeatures, bool overwriteOtherProps)
        {
            if (_ewocUser.IsAdmin == false)
            {
                throw new UnauthorizedAccessException("Admin Auth required.");
            }

            var entity = await _consortiumCollectionsRepository.GetAsync(x => x.Id == id);
            entity.NeedsUpdate = update;
            entity.OverwriteFeatures = overwriteFeatures;
            entity.OverwriteOtherProps = overwriteOtherProps;
            await _consortiumCollectionsRepository.UpdateAsync(entity);
        }

        [HttpDelete("store/ref/collection/{id:int}")]
        public async Task DeleteCollection(int id)
        {
            if (_ewocUser.IsAdmin == false)
            {
                throw new UnauthorizedAccessException("Admin Auth required.");
            }

            await _consortiumCollectionsRepository.DeleteAsync(x => x.Id == id);
        }

        [HttpDelete("store/ref/collection/{collectionId}/database")]
        public async Task<IActionResult> DeleteCollectionDatabase(string collectionId)
        {
            if (_ewocUser.IsAdmin == false)
            {
                throw new UnauthorizedAccessException("Admin Auth required.");
            }

            await _addCollectionHelper.DeleteUserDataset(collectionId);
            return new OkObjectResult("Deleted " + collectionId);
        }
    }
}