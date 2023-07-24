using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Dtos;
using IIASA.WorldCereal.Rdm.Dtos.GeoJson;
using IIASA.WorldCereal.Rdm.Entity;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace IIASA.WorldCereal.Rdm.Services
{
    [RemoteService(Name = "OGC")]
    [ApiExplorerSettings(GroupName = "OGC")]
    public class MasterCollectionStoreService : RdmAppService
    {
        private readonly IRepository<CollectionMetadataEntity, Guid> _masterCollectionRepository;
        private readonly IRepository<ItemEntity, long> _itemRepository;
        private readonly IRepository<CollectionMetadataEntity, Guid> _collectionRepository;
        private readonly IEwocUser _ewocUser;
        private readonly ICurrentTenant _currentTenant;
        private readonly ITenantRepository _tenantRepository;
        private readonly IGeoJsonHelper _geoJsonHelper;
        private readonly IAddCollectionHelper _addCollectionHelper;

        public MasterCollectionStoreService(IRepository<CollectionMetadataEntity, Guid> masterCollectionRepository,
            IRepository<ItemEntity, long> itemRepository,
            IRepository<CollectionMetadataEntity, Guid> collectionRepository, IEwocUser ewocUser,
            ICurrentTenant currentTenant, ITenantRepository tenantRepository, IGeoJsonHelper geoJsonHelper,
            IAddCollectionHelper addCollectionHelper)
        {
            _masterCollectionRepository = masterCollectionRepository;
            _itemRepository = itemRepository;
            _collectionRepository = collectionRepository;
            _ewocUser = ewocUser;
            _currentTenant = currentTenant;
            _tenantRepository = tenantRepository;
            _geoJsonHelper = geoJsonHelper;
            _addCollectionHelper = addCollectionHelper;
        }

        [HttpGet("reference/db/collections")]
        public async Task<IEnumerable<MasterCollectionStoreInfoDto>> Get()
        {
            if (_ewocUser.IsAdmin == false)
            {
                throw new UnauthorizedAccessException("Admin Auth required.");
            }

            var stores = await _masterCollectionRepository.ToListAsync();
            return stores.Select(x => ObjectMapper.Map<CollectionMetadataEntity, MasterCollectionStoreInfoDto>(x));
        }

        /// <summary>
        /// Create Consortium collection
        /// </summary>
        /// <remarks>This creates a collection without any validation and prepares for feature injection.</remarks>
        /// <param name="addMasterCollectionStore"></param>
        [HttpPost("reference/db/collections")]
        public async Task<MasterCollectionStoreInfoDto> Create(AddMasterCollectionStoreInfoDto addMasterCollectionStore)
        {
            if (_ewocUser.IsAdmin == false)
            {
                throw new UnauthorizedAccessException("Admin Auth required.");
            }

            var masterCollectionStoreInfo =
                ObjectMapper.Map<AddMasterCollectionStoreInfoDto, CollectionMetadataEntity>(addMasterCollectionStore);

            var collectionStoreInfo =
                await _addCollectionHelper.CreateMasterCollectionStoreInfo(masterCollectionStoreInfo,
                    CurrentUnitOfWork);

            var tenant = await _tenantRepository.FindByNameAsync(collectionStoreInfo.CollectionId);
            using (_currentTenant.Change(tenant.Id))
            {
                var collectionMetadata =
                    ObjectMapper
                        .Map<CollectionMetadataEntity, CollectionMetadataEntity>(collectionStoreInfo);
                collectionMetadata.TenantId = tenant.Id;
                await _masterCollectionRepository.InsertAsync(collectionMetadata);
            }

            return ObjectMapper.Map<CollectionMetadataEntity, MasterCollectionStoreInfoDto>(collectionStoreInfo);
        }

        [HttpPut("reference/db/collections/{collectionId}/items")]
        public async Task AddItems(string collectionId, [FromBody] FeatureCollectionGeoJSON featureCollection)
        {
            if (_ewocUser.IsAdmin == false)
            {
                throw new UnauthorizedAccessException("Admin Auth required.");
            }

            var col = await _collectionRepository.GetAsync(x => x.CollectionId == collectionId);
            var tenant = await _tenantRepository.FindByNameAsync(col.CollectionId);
            var items = _geoJsonHelper.GetItems(featureCollection, col.CollectionId);

            using (_currentTenant.Change(tenant.Id))
            {
                await _itemRepository.InsertManyAsync(items);
            }
        }
    }
}