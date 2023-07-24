using System;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Dtos;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.Enums;
using IIASA.WorldCereal.Rdm.Mappings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.TenantManagement;

namespace IIASA.WorldCereal.Rdm.Services
{
    [RemoteService(Name = "OGC")]
    [ApiExplorerSettings(GroupName = "OGC")]
    public class CollectionService : RdmAppService
    {
        private readonly IRepository<CollectionMetadataEntity, Guid> _collectionMetadataRepository;
        private readonly IRepository<CollectionMetadataEntity, Guid> _masterCollectionRepository;
        private readonly IGeoJsonHelper _geoJsonHelper;
        private readonly IRepository<UserDataset, Guid> _userDatasetRepository;
        private readonly IEwocUser _ewocUser;
        private readonly ILogger<CollectionService> _logger;
        private readonly ITenantRepository _tenantRepository;

        public CollectionService(IRepository<CollectionMetadataEntity, Guid> collectionMetadataRepository,
            IRepository<CollectionMetadataEntity, Guid> masterCollectionRepository, IGeoJsonHelper geoJsonHelper,
            IRepository<UserDataset, Guid> userDatasetRepository, IEwocUser ewocUser,
            ILogger<CollectionService> logger,
            ITenantRepository tenantRepository)
        {
            _collectionMetadataRepository = collectionMetadataRepository;
            _masterCollectionRepository = masterCollectionRepository;
            _geoJsonHelper = geoJsonHelper;
            _userDatasetRepository = userDatasetRepository;
            _ewocUser = ewocUser;
            _logger = logger;
            _tenantRepository = tenantRepository;
        }


        [HttpGet("/")]
        public IActionResult Get()
        {
            _logger.LogInformation($"Getting root path");
            dynamic data = new ExpandoObject();
            data.Title = "World Cereal";
            data.Description = "World Cereal Reference Data Module.";
            return new OkObjectResult(data);
        }

        [HttpGet("conformance")]
        public IActionResult GetConformance()
        {
            _logger.LogInformation($"Getting conformance");
            dynamic data = new ExpandoObject();
            data.conformsTo = new[] { "To be decided." };
            return new OkObjectResult(data);
        }

        [HttpGet("collections")]
        public async Task<PagedResultDto<CollectionMetadataDto>> GetList(PagedResultRequestDto pagedResultRequestDto)
        {
            var allowedAccess = GetAllowedAccess();

            _logger.LogInformation("Getting collections-");
            var entities = await _masterCollectionRepository
                .Where(x => allowedAccess.Contains(x.AccessType))
                .OrderBy(x => x.CollectionId)
                .Skip(pagedResultRequestDto.SkipCount)
                .Take(pagedResultRequestDto.MaxResultCount).ToListAsync();

            var count = await _masterCollectionRepository.Where(x => allowedAccess.Contains(x.AccessType)).CountAsync();

            return new PagedResultDto<CollectionMetadataDto>(count,
                entities.Select(x => ObjectMapper.Map<CollectionMetadataEntity, CollectionMetadataDto>(x)).ToArray());
        }

        [HttpGet("collections/map")]
        public async Task<IActionResult> GetCollectionGeojson([FromQuery] StoreType storeType = StoreType.Reference,
            [FromQuery] int year = 0)
        {
            _logger.LogInformation("Getting collections-");
            var allowedAccess = GetAllowedAccess();

            var collections = await GetFilteredCollection(allowedAccess, storeType, year);

            var mapCollections = collections
                .Select(x => new CollectionMapData
                {
                    Title = x.Title, Type = x.Type, CollectionId = x.CollectionId, FeatureCount = x.FeatureCount,
                    StoreType = x.StoreType, AccessType = x.AccessType,
                    Extent = x.Extent
                }).ToList();

            var geoJsonString = _geoJsonHelper.GetCollections(mapCollections);
            dynamic jObject = JsonConvert.DeserializeObject(geoJsonString);
            return GeoJsonHttpHelper.GetContentResult(jObject);
        }

        private async Task<CollectionMetadataEntity[]> GetFilteredCollection(AccessType[] allowedAccess,
            StoreType storeType, int year)
        {
            var query = _masterCollectionRepository
                .Where(x => allowedAccess.Contains(x.AccessType) && x.StoreType == storeType);

            var collections = await _masterCollectionRepository
                .Where(x => query.Select(s => s.Id)
                    .Contains(x.Id))
                .ToListAsync();
            return collections.Where(x => year == 0 ||
                                          x.FirstDateOfValidityTime.Year == year ||
                                          x.LastDateOfValidityTime.Year == year).ToArray();
        }

        [HttpGet("collections/stats")]
        public async Task<IActionResult> GetCollectionStats([FromQuery] StoreType storeType = StoreType.Reference,
            [FromQuery] int year = 0)
        {
            var allowedAccess = GetAllowedAccess();

            var collections = await GetFilteredCollection(allowedAccess, storeType, year);

            return new OkObjectResult(new
                { TotalCollections = collections.Length, FeaturesTotalCount = collections.Sum(x => x.FeatureCount) });
        }

        [HttpGet("collections/{collectionId}")]
        public async Task<CollectionMetadataDto> Get(string collectionId)
        {
            _logger.LogInformation($"Getting collections for -{collectionId}");
            var tenant = await GetTenant(collectionId);
            using (CurrentTenant.Change(tenant.Id))
            {
                var entity = await _collectionMetadataRepository.FirstOrDefaultAsync();
                return ObjectMapper.Map<CollectionMetadataEntity, CollectionMetadataDto>(entity);
            }
        }

        [HttpGet("collections/{collectionId}/available")]
        public async Task<bool> CheckIfCollectionIdIsAvailable(string collectionId)
        {
            _logger.LogInformation($"Checking if {collectionId} is available");
            var isCollectionIdTakenAlready =
                await _masterCollectionRepository.AnyAsync(x => x.CollectionId == collectionId);
            if (isCollectionIdTakenAlready)
            {
                return false;
            }

            isCollectionIdTakenAlready = await _userDatasetRepository.AnyAsync(x => x.CollectionId == collectionId);

            return !isCollectionIdTakenAlready;
        }

        private AccessType[] GetAllowedAccess()
        {
            var allowedAccess = new[] { AccessType.Public };
            if (_ewocUser.IsAdmin)
            {
                allowedAccess = new[] { AccessType.Public, AccessType.Private };
            }

            return allowedAccess;
        }

        private async Task<Tenant> GetTenant(string collectionId)
        {
            _logger.LogInformation($"Getting tenant for-{collectionId}");
            var tenant = await _tenantRepository.FindByNameAsync(collectionId);
            if (tenant == null)
            {
                throw new EntityNotFoundException(
                    "Tenant/Collection Not Found. Please create using MasterCollectionStore API.");
            }

            return tenant;
        }
    }
}