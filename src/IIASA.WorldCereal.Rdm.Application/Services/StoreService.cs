using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Dtos;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace IIASA.WorldCereal.Rdm.Services
{
    [RemoteService(Name = "OGC")]
    [ApiExplorerSettings(GroupName = "OGC")]
    [Authorize]
    public class StoreService : RdmAppService
    {
        private readonly IRepository<StoreEntity, Guid> _storeRepository;
        private readonly IAddCollectionHelper _addCollectionHelper;
        private readonly IRepository<ConsortiumCollection> _consortiumCollectionsRepository;
        private IAccessProvider _accessProvider;

        public StoreService(IRepository<StoreEntity, Guid> storeRepository, IAddCollectionHelper addCollectionHelper,
            IRepository<ConsortiumCollection> consortiumCollectionsRepository, IAccessProvider accessProvider
        )
        {
            _accessProvider = accessProvider;
            _storeRepository = storeRepository;
            _addCollectionHelper = addCollectionHelper;
            _consortiumCollectionsRepository = consortiumCollectionsRepository;
        }

        [HttpGet("store")]
        public async Task<IEnumerable<StoreDto>> Get()
        {
            var stores = await _storeRepository.GetListAsync();
            return stores.Select(x => ObjectMapper.Map<StoreEntity, StoreDto>(x));
        }

        [HttpGet("user/isadmin")]
        public async Task<bool> GetIsAdmin()
        {
            return await _accessProvider.IsAdmin();
        }

        [HttpGet("user/ismoderator")]
        public async Task<bool> GetIsModerator()
        {
            return (await _accessProvider.User()).UserRole == UserRole.Moderator;
        }

        [HttpGet("user/role")]
        public async Task<string> GetRole()
        {
            try
            {
                return (await _accessProvider.User()).UserRole.ToString("G");
            }
            catch (Exception e)
            {
                return UserRole.User.ToString("G");
            }
        }

        [HttpGet("user/id")]
        public async Task<string> GetUserId()
        {
            return (await _accessProvider.User()).UserId.ToString("D");
        }

        [HttpPost("store")]
        public async Task<StoreDto> Create(AddStoreDto storeDto)
        {
            if (await _accessProvider.IsAdmin() == false)
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
            if (await _accessProvider.IsAdmin() == false)
            {
                throw new UnauthorizedAccessException("Admin Auth required.");
            }

            var stores = await _consortiumCollectionsRepository.GetListAsync();
            return stores.Select(x => ObjectMapper.Map<ConsortiumCollection, ConsortiumCollectionDto>(x));
        }

        [HttpPost("store/ref/collection")]
        public async Task<IActionResult> AddCollection(AddUrlsForConsortiumUpload upload)
        {
            if (await _accessProvider.IsAdmin() == false)
            {
                throw new UnauthorizedAccessException("Admin Auth required.");
            }

            var entities = new List<ConsortiumCollection>();
            foreach (var url in upload.Urls)
            {
                entities.Add(new ConsortiumCollection(){DownloadZipUrl = url,NeedsUpdate = upload.NeedsUpdate});
            }

            await _consortiumCollectionsRepository.InsertManyAsync(entities);
            return new OkObjectResult(new
            {
                Result = ObjectMapper.Map<ConsortiumCollection[], ConsortiumCollectionDto[]>(entities.ToArray()),
                Message = "Added collection to table. Next you need to run the ref db update job in k8s."
            });
        }

        [HttpPut("store/ref/collection/{id:int}")]
        public async Task UpdateCollection(int id, bool update, bool overwriteFeatures, bool overwriteOtherProps)
        {
            if (await _accessProvider.IsAdmin() == false)
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
            if (await _accessProvider.IsAdmin() == false)
            {
                throw new UnauthorizedAccessException("Admin Auth required.");
            }

            await _consortiumCollectionsRepository.DeleteAsync(x => x.Id == id);
        }

        [HttpDelete("store/ref/collection/{collectionId}/database")]
        public async Task<IActionResult> DeleteCollectionDatabase(string collectionId)
        {
            if (await _accessProvider.IsAdmin() == false)
            {
                throw new UnauthorizedAccessException("Admin Auth required.");
            }

            await _addCollectionHelper.DeleteUserDataset(collectionId);
            return new OkObjectResult("Deleted " + collectionId);
        }
    }
}