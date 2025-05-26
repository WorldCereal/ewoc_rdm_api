using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Core.Vector;
using IIASA.WorldCereal.Rdm.Dtos;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.Enums;
using IIASA.WorldCereal.Rdm.ExcelOps;
using IIASA.WorldCereal.Rdm.Mappings;
using IIASA.WorldCereal.Rdm.ServiceConfigs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
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
        private readonly IRepository<MetadataItem, int> _metadataItemsRepository;
        private readonly IAzureFileUploader _azureFileUploader;
        private readonly ILogger<CollectionService> _logger;
        private readonly ICurrentTenant _currentTenant;
        private readonly IAccessProvider _accessProvider;
        private readonly IRepository<ItemEntity, long> _itemRepository;
        private readonly UserDatasetConfig _userDatasetConfig;
        private readonly ITenantRepository _tenantRepository;

        public CollectionService(IRepository<CollectionMetadataEntity, Guid> collectionMetadataRepository,
            IRepository<CollectionMetadataEntity, Guid> masterCollectionRepository, IGeoJsonHelper geoJsonHelper,
            IRepository<UserDataset, Guid> userDatasetRepository,
            IRepository<MetadataItem, int> metadataItemsRepository, IAzureFileUploader azureFileUploader,
            ILogger<CollectionService> logger, ICurrentTenant currentTenant, IAccessProvider accessProvider,
            IRepository<ItemEntity, long> itemRepository, UserDatasetConfig userDatasetConfig,
            ITenantRepository tenantRepository)
        {
            _collectionMetadataRepository = collectionMetadataRepository;
            _masterCollectionRepository = masterCollectionRepository;
            _geoJsonHelper = geoJsonHelper;
            _userDatasetRepository = userDatasetRepository;
            _metadataItemsRepository = metadataItemsRepository;
            _azureFileUploader = azureFileUploader;
            _logger = logger;
            _currentTenant = currentTenant;
            _accessProvider = accessProvider;
            _itemRepository = itemRepository;
            _userDatasetConfig = userDatasetConfig;
            _tenantRepository = tenantRepository;
        }


        /*[HttpGet("/")]
        public IActionResult Get()
        {
            _logger.LogInformation($"Getting root path");
            dynamic data = new ExpandoObject();
            data.Title = "World Cereal";
            data.Description = "World Cereal Reference Data Module.";
            return new OkObjectResult(data);
        }

        [HttpGet("conformance")]
        public async Task<IActionResult> GetConformance()
        {
            _logger.LogInformation($"Getting conformance");
            dynamic data = new ExpandoObject();
            data.conformsTo = new[] { "To be decided." };
            return new OkObjectResult(data);
        }*/

        [HttpGet("collections")]
        public async Task<PagedResultDto<CollectionMetadataDto>> GetList(PagedResultRequestDto pagedResultRequestDto)
        {
            var communityAllowedAccess = await _accessProvider.GetCommunityAllowedAccess();
            var consortiumAllowedAccess = await _accessProvider.GetConsortiumAllowedAccess();
            var userCol = await GetUserDatasets();

            _logger.LogInformation("Getting collections-");
            var entities = await (await _masterCollectionRepository.GetQueryableAsync())
                .Where(x => (x.StoreType == StoreType.Community && communityAllowedAccess.Contains(x.AccessType)) ||
                            (x.StoreType == StoreType.Reference &&
                             consortiumAllowedAccess.Contains(x.AccessType)) || userCol.Contains(x.CollectionId))
                .OrderBy(x => x.CollectionId)
                .Skip(pagedResultRequestDto.SkipCount)
                .Take(pagedResultRequestDto.MaxResultCount).AsNoTracking().ToListAsync();

            var count = await (await _masterCollectionRepository.GetQueryableAsync())
                .Where(x => (x.StoreType == StoreType.Community && communityAllowedAccess.Contains(x.AccessType)) ||
                            (x.StoreType == StoreType.Reference &&
                             consortiumAllowedAccess.Contains(x.AccessType)) || userCol.Contains(x.CollectionId))
                .CountAsync();

            return new PagedResultDto<CollectionMetadataDto>(count,
                entities.Select(x => ObjectMapper.Map<CollectionMetadataEntity, CollectionMetadataDto>(x)).ToArray());
        }

        [HttpGet("collections/map")]
        public async Task<IActionResult> GetCollectionGeojson([FromQuery] MapSearch mapSearch)
        {
            _logger.LogInformation("Getting collections-");
            var communityAllowedAccess = await _accessProvider.GetCommunityAllowedAccess();
            var consortiumAllowedAccess = await _accessProvider.GetConsortiumAllowedAccess();
            var userCol = await GetUserDatasets();

            CollectionMetadataEntity[] collections;
            if (string.IsNullOrWhiteSpace(mapSearch.EwocCodes))
            {
                collections = await GetFilteredCollection(communityAllowedAccess, consortiumAllowedAccess,
                    mapSearch.Year, userCol);
            }
            else
            {
                collections = await GetFilteredCollectionWithEwocCodes(communityAllowedAccess, consortiumAllowedAccess,
                    mapSearch.EwocCodes.Split(",").Select(long.Parse).ToArray(), mapSearch.Year);
            }

            var mapCollections = collections
                .Select(x => new CollectionMapData
                {
                    Title = x.Title, Type = x.Type, CollectionId = x.CollectionId, FeatureCount = x.FeatureCount,
                    StoreType = x.StoreType, AccessType = x.AccessType,
                    Extent = x.Extent, EwocCodes = x.EwocCodes
                }).ToList();

            var geoJsonString = _geoJsonHelper.GetCollections(mapCollections);
            dynamic jObject = JsonConvert.DeserializeObject(geoJsonString) ?? new { };
            return GeoJsonHttpHelper.GetContentResult(jObject);
        }

        private async Task<CollectionMetadataEntity[]> GetFilteredCollection(AccessType[] communityAccessTypes,
            AccessType[] consortiumAccessTypes, int year,
            string[] userCol)
        {
            var collections = await (await _masterCollectionRepository.GetQueryableAsync())
                .Where(x => (x.StoreType == StoreType.Community && communityAccessTypes.Contains(x.AccessType)) ||
                            (x.StoreType == StoreType.Reference &&
                             consortiumAccessTypes.Contains(x.AccessType)) || userCol.Contains(x.CollectionId))
                .AsNoTracking()
                .ToListAsync();
            return collections.Where(x => year == 0 ||
                                          x.FirstDateOfValidityTime.Year == year ||
                                          x.LastDateOfValidityTime.Year == year).ToArray();
        }

        private async Task<CollectionMetadataEntity[]> GetFilteredCollectionWithEwocCodes(
            AccessType[] communityAccessTypes, AccessType[] consortiumAccessTypes,
            long[] ewocCodes, int year)
        {
            var collections = await (await _masterCollectionRepository.GetQueryableAsync())
                .Where(x => ((x.StoreType == StoreType.Community && communityAccessTypes.Contains(x.AccessType)) ||
                             (x.StoreType == StoreType.Reference &&
                              consortiumAccessTypes.Contains(x.AccessType))) &&
                            x.EwocCodes.Intersect(ewocCodes).Any()).AsNoTracking()
                .ToListAsync();
            return collections.Where(x => year == 0 ||
                                          x.FirstDateOfValidityTime.Year == year ||
                                          x.LastDateOfValidityTime.Year == year).ToArray();
        }

        [HttpGet("collections/stats")]
        public async Task<CollectionStats> GetCollectionStats([FromQuery] StoreType storeType = StoreType.All,
            [FromQuery] int year = 0, [FromQuery] string ewocCodes = "")
        {
            var communityAllowedAccess = await _accessProvider.GetCommunityAllowedAccess();
            var consortiumAllowedAccess = await _accessProvider.GetConsortiumAllowedAccess();
            var userCols = await GetUserDatasets();
            CollectionMetadataEntity[] collections;

            if (storeType == StoreType.All || year == 0)
            {
                collections = await (await _masterCollectionRepository.GetQueryableAsync())
                    .Where(x => (x.StoreType == StoreType.Community && communityAllowedAccess.Contains(x.AccessType)) ||
                                (x.StoreType == StoreType.Reference &&
                                 consortiumAllowedAccess.Contains(x.AccessType)) || userCols.Contains(x.CollectionId))
                    .ToArrayAsync();
                if (year > 0)
                {
                    collections = collections.Where(x => x.FirstDateOfValidityTime.Year == year ||
                                                         x.LastDateOfValidityTime.Year == year).ToArray();
                }

                if (storeType != StoreType.All)
                {
                    collections = collections.Where(x => x.StoreType == storeType).ToArray();
                }
            }
            else
            {
                collections =
                    await GetFilteredCollection(communityAllowedAccess, consortiumAllowedAccess, year, userCols);
            }

            if (string.IsNullOrWhiteSpace(ewocCodes) == false)
            {
                var filterCodes = ewocCodes.Split(",").Select(x => long.Parse(x)).ToArray();
                collections = collections.Where(x => x.EwocCodes.Any(e => filterCodes.Contains(e))).ToArray();
            }

            return new
                CollectionStats
                { TotalCollections = collections.Length, FeaturesTotalCount = collections.Sum(x => x.FeatureCount) };
        }

        [HttpGet("collections/{collectionId}")]
        public async Task<CollectionMetadataDto> Get(string collectionId)
        {
            _logger.LogInformation($"Getting collections for -{collectionId}");
            var tenant = await GetTenant(collectionId);
            using (CurrentTenant.Change(tenant.Id))
            {
                var entity = await _masterCollectionRepository.FirstAsync();
                return ObjectMapper.Map<CollectionMetadataEntity, CollectionMetadataDto>(entity);
            }
        }

        [HttpGet("collections/{collectionId}/license")]
        public async Task<IActionResult> GetCollectionLicense(string collectionId)
        {
            _logger.LogInformation($"Getting metadata for -{collectionId}");
            var tenant = await GetTenant(collectionId);
            var licenseMarkers = new[]
            {
                ExcelMarkers.TypeOfLicenseAccessType, ExcelMarkers.ReferenceToLicense, ExcelMarkers.RequiredCitation
            };
            using (CurrentTenant.Change(tenant.Id))
            {
                var items = await (await _metadataItemsRepository.GetQueryableAsync())
                    .Where(x => licenseMarkers.Contains(x.Name)).AsNoTracking()
                    .ToArrayAsync();

                var typeOfLicense = items.FirstOrDefault(x => x.Name == ExcelMarkers.TypeOfLicenseAccessType);
                var refToLicense = items.FirstOrDefault(x => x.Name == ExcelMarkers.ReferenceToLicense);
                var citation = items.FirstOrDefault(x => x.Name == ExcelMarkers.RequiredCitation);
                var type = typeOfLicense != null ? typeOfLicense.Value : "NA";
                var link = HasLicenseLink(type);
                if (string.IsNullOrEmpty(link))
                {
                    return new OkObjectResult(new
                    {
                        TypeOfLicenseAccess = type,
                        ReferenceToLicense = refToLicense != null ? refToLicense.Value : "NA",
                        RequiredCitation = citation != null ? citation.Value : "NA",
                    });
                }
                else
                {
                    return new OkObjectResult(new
                    {
                        TypeOfLicenseAccess = type,
                        LinkToLicense = link,
                        ReferenceToLicense = refToLicense != null ? refToLicense.Value : "NA",
                        RequiredCitation = citation != null ? citation.Value : "NA",
                    });
                }
            }
        }


        [HttpGet("collections/{collectionId}/download")]
        public async Task<IActionResult> Download(string collectionId, [FromQuery] bool force)
        {
            _logger.LogInformation($"Downloading collections for -{collectionId}");
            var communityAllowedAccess = await _accessProvider.GetCommunityAllowedAccess();
            var consortiumAllowedAccess = await _accessProvider.GetConsortiumAllowedAccess();
            var collectionMetadataEntity =
                await GetColData(collectionId, communityAllowedAccess, consortiumAllowedAccess);
            _logger.LogInformation(
                $"Downloading started for public collections for -{collectionMetadataEntity.CollectionId}");
            return await GetGeoParquetDownloadUrl(collectionMetadataEntity.CollectionId,
                ExcelMarkers.GeoParquetDownloadUrl, force);
        }

        [HttpPut("collections/{collectionId}/download")]
        public async Task UpdatePublicDatasetDownloadUrl(string collectionId, string url)
        {
            if (await _accessProvider.IsAdmin())
            {
                var tenant = await GetTenant(collectionId);
                using (_currentTenant.Change(tenant.Id))
                {
                    var urlMetadataItem = await (await _metadataItemsRepository.GetQueryableAsync())
                        .FirstOrDefaultAsync(x =>
                            x.Name == ExcelMarkers.GeoParquetDownloadUrl);
                    await InsertOrUpdate(ExcelMarkers.GeoParquetDownloadUrl, urlMetadataItem, url, tenant.Id);
                }
            }
        }

        [HttpPut("collections/{collectionId}/sample/download")]
        public async Task UpdatePublicDatasetSampleDownloadUrl(string collectionId, string url)
        {
            if (await _accessProvider.IsAdmin())
            {
                var tenant = await GetTenant(collectionId);
                using (_currentTenant.Change(tenant.Id))
                {
                    var urlMetadataItem = await (await _metadataItemsRepository.GetQueryableAsync())
                        .FirstOrDefaultAsync(x =>
                            x.Name == ExcelMarkers.SampleDownloadUrl);
                    await InsertOrUpdate(ExcelMarkers.SampleDownloadUrl, urlMetadataItem, url, tenant.Id);
                }
            }
        }

        private async Task<CollectionMetadataEntity> GetColData(string collectionId,
            AccessType[] communityAllowedAccess, AccessType[] consortiumAllowedAccess)
        {
            var userCol = await GetUserDatasets();
            var col = await _masterCollectionRepository.FirstAsync(x =>
                x.CollectionId == collectionId &&
                (((x.StoreType == StoreType.Community && communityAllowedAccess.Contains(x.AccessType)) ||
                  (x.StoreType == StoreType.Reference &&
                   consortiumAllowedAccess.Contains(x.AccessType))) || userCol.Contains(x.CollectionId)));
            return col;
        }

        private async Task<IActionResult> GetGeoParquetDownloadUrl(string collectionId, string typeOfUrl, bool force)
        {
            var tenant = await GetTenant(collectionId);
            var tempPath = Path.Combine(_userDatasetConfig.TempFolder, Guid.NewGuid().ToString("N"));

            var filePath = string.Empty;
            var url = string.Empty;
            using (_currentTenant.Change(tenant.Id))
            {
                var entity = await _collectionMetadataRepository.FirstAsync();
                var colfileName = collectionId + ".geoparquet";
                colfileName = typeOfUrl == ExcelMarkers.SampleDownloadUrl ? $"sample_{colfileName}" : colfileName;
                if (!force && entity is { AccessType: AccessType.Private })
                {
                    url = await _azureFileUploader.PrivateFileExists(collectionId, colfileName);
                    if (string.IsNullOrEmpty(url) == false)
                    {
                        return new OkObjectResult(url);
                    }
                }

                var urlMetadataItem = await (await _metadataItemsRepository.GetQueryableAsync())
                    .FirstOrDefaultAsync(x =>
                        x.Name == typeOfUrl);
                if (!force && entity.AccessType == AccessType.Public && urlMetadataItem != default)
                {
                    return new OkObjectResult(urlMetadataItem.Value);
                }

                if (!force && entity.AccessType == AccessType.Public)
                {
                    url = await _azureFileUploader.PublicFileExists(collectionId, colfileName);
                    if (string.IsNullOrEmpty(url) == false)
                    {
                        await InsertOrUpdate(typeOfUrl, urlMetadataItem, url, tenant.Id);
                        return new OkObjectResult(url);
                    }
                }

                var items = typeOfUrl == ExcelMarkers.GeoParquetDownloadUrl
                    ? await (await _itemRepository.GetQueryableAsync()).OrderBy(x => x.Id).AsNoTracking().ToListAsync()
                    : await (await _itemRepository.GetQueryableAsync()).Where(x => x.Extract != 0)
                        .OrderBy(x => x.Id).AsNoTracking().ToListAsync();

                if (items.Any() == false)
                {
                    throw new EntityNotFoundException($"No items found for {typeOfUrl}");
                }

                var bbox = (await _collectionMetadataRepository.FirstAsync()).Extent.Coordinates;
                filePath = await GeoParquetWriter.WriteDataset(items, tempPath, collectionId, bbox, false);
                var fileName = new FileInfo(filePath).Name;
                var name = typeOfUrl == ExcelMarkers.SampleDownloadUrl ? $"sample_{fileName}" : fileName;
                await using var fileStream = File.OpenRead(filePath);
                url = entity.AccessType == AccessType.Public
                    ? await _azureFileUploader.UploadPublicFileAsync(collectionId, fileStream,
                        name)
                    : await _azureFileUploader.UploadPrivateFileAsync(collectionId, fileStream, name);
                await InsertOrUpdate(typeOfUrl, urlMetadataItem, url, tenant.Id);
            }

            Directory.Delete(tempPath, true);

            if (string.IsNullOrEmpty(url))
            {
                throw new ApplicationException("Unable to generate GeoParquet file");
            }

            return new OkObjectResult(url);
        }

        private async Task InsertOrUpdate(string typeOfUrl, MetadataItem? urlMetadataItem, string url, Guid tenantId)
        {
            if (urlMetadataItem != default)
            {
                urlMetadataItem.Value = url;
                await _metadataItemsRepository.UpdateAsync(urlMetadataItem);
            }
            else
            {
                await _metadataItemsRepository.InsertAsync(new MetadataItem()
                    { Name = typeOfUrl, Value = url, TenantId = tenantId });
            }
        }

        [HttpGet("collections/{collectionId}/sample/download")]
        public async Task<IActionResult> DownloadSamples(string collectionId, [FromQuery] bool force)
        {
            _logger.LogInformation($"Downloading collections for -{collectionId}");
            var communityAllowedAccess = await _accessProvider.GetCommunityAllowedAccess();
            var consortiumAllowedAccess = await _accessProvider.GetConsortiumAllowedAccess();
            var col = await GetColData(collectionId, communityAllowedAccess, consortiumAllowedAccess);
            _logger.LogInformation($"Downloading started for public collections for -{col.CollectionId}");
            return await GetGeoParquetDownloadUrl(col.CollectionId, ExcelMarkers.SampleDownloadUrl, force);
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

        private async Task<string[]> GetUserDatasets()
        {
            var userCol = Array.Empty<string>();
            if (_accessProvider.IsLoggedIn())
            {
                var userId = await _accessProvider.UserId();
                userCol = await (await _userDatasetRepository.GetQueryableAsync()).Where(x => x.CreatorId == userId)
                    .Select(x => x.CollectionId).ToArrayAsync();
            }

            return userCol;
        }

        private string HasLicenseLink(string data)
        {
            if (data == "CC_BY")
            {
                return "https://creativecommons.org/licenses/by/4.0/";
            }

            if (data == "CC_BY_SA")
            {
                return "https://creativecommons.org/licenses/by-sa/4.0/";
            }

            if (data == "CC_BY_NC")
            {
                return "https://creativecommons.org/licenses/by-nc/4.0/";
            }

            if (data == "CC_BY_NC_SA")
            {
                return "https://creativecommons.org/licenses/by-nc-sa/4.0/";
            }

            return string.Empty;
        }
    }
}