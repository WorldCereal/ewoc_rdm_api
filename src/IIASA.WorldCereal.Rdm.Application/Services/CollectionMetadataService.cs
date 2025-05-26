using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Core.Vector;
using IIASA.WorldCereal.Rdm.Dtos;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.Enums;
using IIASA.WorldCereal.Rdm.ExcelOps;
using IIASA.WorldCereal.Rdm.Mappings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.TenantManagement;

namespace IIASA.WorldCereal.Rdm.Services
{
    [RemoteService(Name = "OGC")]
    [ApiExplorerSettings(GroupName = "OGC")]
    public class CollectionMetadataService : RdmAppService
    {
        private readonly IRepository<MetadataItem, int> _metadataItemsRepository;
        private readonly IRepository<DatasetEvent, int> _datasetEventRepository;
        private readonly IAddCollectionHelper _addCollectionHelper;
        private readonly IRepository<CollectionMetadataEntity> _collectionMetadataRepository;
        private readonly ILogger<CollectionMetadataService> _logger;
        private readonly IRepository<EoType, int> _eoTypeRepository;
        private readonly IDownloadCountTableHelper _downloadCountTableHelper;
        private readonly IRepository<ItemEntity, long> _itemRepository;
        private readonly ITenantRepository _tenantRepository;
        private IAccessProvider _accessProvider;

        public CollectionMetadataService(IRepository<MetadataItem, int> metadataItemsRepository,
            IRepository<DatasetEvent, int> datasetEventRepository,IAddCollectionHelper addCollectionHelper,
            IRepository<CollectionMetadataEntity> collectionMetadataRepository, IAccessProvider accessProvider,
            ILogger<CollectionMetadataService> logger, IRepository<EoType, int> eoTypeRepository,
            IDownloadCountTableHelper downloadCountTableHelper, IRepository<ItemEntity, long> itemRepository,
            ITenantRepository tenantRepository)
        {
            _accessProvider = accessProvider;
            _metadataItemsRepository = metadataItemsRepository;
            _datasetEventRepository = datasetEventRepository;
            _addCollectionHelper = addCollectionHelper;
            _collectionMetadataRepository = collectionMetadataRepository;
            _logger = logger;
            _eoTypeRepository = eoTypeRepository;
            _downloadCountTableHelper = downloadCountTableHelper;
            _itemRepository = itemRepository;
            _tenantRepository = tenantRepository;
        }

        [HttpGet("eotypes/{type}")]
        public async Task<EoTypeDto[]> GetType(CodeType type)
        {
            var types = await (await _eoTypeRepository.GetQueryableAsync()).Where(x => x.CodeType == type)
                .AsNoTracking()
                .ToArrayAsync();
            return ObjectMapper.Map<EoType[], EoTypeDto[]>(types);
        }

        [HttpGet("collections/{collectionId}/metadata/items")]
        public async Task<MetadataItemDto[]> GetMetadata(string collectionId)
        {
            _logger.LogInformation($"Getting metadata for -{collectionId}");
            var tenant = await GetTenant(collectionId);
            using (CurrentTenant.Change(tenant.Id))
            {
                var items = await (await _metadataItemsRepository.GetQueryableAsync()).AsNoTracking().ToArrayAsync();
                return ObjectMapper.Map<MetadataItem[], MetadataItemDto[]>(items);
            }
        }

        [HttpGet("collections/{collectionId}/download/counts")]
        public async Task<DownloadCounts> GetDownloadCounts(string collectionId)
        {
            _logger.LogInformation($"Getting DownloadCounts for -{collectionId}");
            return await _downloadCountTableHelper.GetDownloadCounts(collectionId);
        }

        [HttpPut("collections/{collectionId}/download/counts")]
        public async Task<DownloadCounts> UpdateCount(string collectionId, [FromBody] string itemName)
        {
            _logger.LogInformation($"Update Count  for -{collectionId}");
            return await _downloadCountTableHelper.UpdateCount(collectionId, itemName);
        }
        
        /*[Authorize]
        [HttpPost("collections/{collectionId}/metadata/items")]
        public async Task CreateManyMetadataItem(string collectionId, [FromBody] MetadataItemDto[] metadataItemDtos)
        {
            _logger.LogInformation($"Creating metadata items for {collectionId}");
            var tenant = await GetTenant(collectionId);
            using (CurrentTenant.Change(tenant.Id))
            {
                var entities = ObjectMapper.Map<MetadataItemDto[], MetadataItem[]>(metadataItemDtos);
                foreach (var metadataItem in entities)
                {
                    metadataItem.TenantId = tenant.Id;
                }

                await _metadataItemsRepository.InsertManyAsync(entities);
            }
        }*/


        [Authorize]
        [HttpPost("collections/{collectionId}/metadata/items")]
        public async Task<MetadataItemDto[]> CreateMetadataItems(string collectionId,
            [FromBody] MetadataItemDto[] metadataItemDtos)
        {
            _logger.LogInformation($"Creating metadata items for -{collectionId}");
            var tenant = await GetTenant(collectionId);
            using (CurrentTenant.Change(tenant.Id))
            {
                var entities = ObjectMapper.Map<MetadataItemDto[], MetadataItem[]>(metadataItemDtos);
                foreach (var entity in entities)
                {
                    entity.TenantId = tenant.Id;
                }

                var names = metadataItemDtos.Select(x => x.Name);
                await _metadataItemsRepository.DeleteAsync(x => names.Contains(x.Name),true);
                await _metadataItemsRepository.InsertManyAsync(entities,true);
                return ObjectMapper.Map<MetadataItem[], MetadataItemDto[]>((await _metadataItemsRepository.GetListAsync(x=>names.Contains(x.Name))).ToArray());
            }
        }

        [Authorize]
        [HttpPost("collections/{collectionId}/metadata/upload")]
        public async Task<IActionResult> UploadMetadataExcel(string collectionId, IFormFile excelBook)
        {
            _logger.LogInformation($"Uploading metadata excel for -{collectionId}");
            Tenant tenant = await GetTenant(collectionId);
            List<ExcelMetadata> excelMetadatas;
            using (Stream stream = excelBook.OpenReadStream())
            {
                excelMetadatas = ExcelOps.ExcelOps.ExtractCollectionMetadata(stream);
            }

            // get dataset metadata
            MetadataItem[] metadataItems = excelMetadatas.Select(x =>
                new MetadataItem { Name = x.Name, Value = x.Value }).ToArray();
            string title = metadataItems.First(x => x.Name == ExcelMarkers.DatasetTitle).Value;

            var confidenceLandCover = ItemsCodeStatsHelper.GetValue(excelMetadatas, ExcelMarkers.ConfidenceLandCover);
            var confidenceCropType = ItemsCodeStatsHelper.GetValue(excelMetadatas, ExcelMarkers.ConfidenceCropType);
            var confidenceIrrigationType =
                ItemsCodeStatsHelper.GetValue(excelMetadatas, ExcelMarkers.ConfidenceIrrigation);

            var accessType = excelMetadatas.First(x => x.Name == ExcelMarkers.TypeOfLicenseAccessType).Value
                .ToLowerInvariant() == "private"
                ? AccessType.Private
                : AccessType.Public;

            // update tenant dataset metadata
            using (CurrentTenant.Change(tenant.Id))
            {
                await InsertOrUpdateDb(metadataItems, tenant.Id);

                CollectionMetadataEntity collectionDefaultMetadata = await _collectionMetadataRepository.FirstAsync();
                collectionDefaultMetadata.Title = title;
                collectionDefaultMetadata.AccessType = accessType;
                collectionDefaultMetadata.ConfidenceLandCover = confidenceLandCover;
                collectionDefaultMetadata.ConfidenceCropType = confidenceCropType;
                collectionDefaultMetadata.ConfidenceIrrigationType = confidenceIrrigationType;
                await _collectionMetadataRepository.UpdateAsync(collectionDefaultMetadata);
            }

            // update master dataset metadata
            CollectionMetadataEntity materCollection =
                await _collectionMetadataRepository.FirstAsync(x => x.CollectionId == collectionId);
            materCollection.Title = title;
            materCollection.AccessType = accessType;
            materCollection.ConfidenceLandCover = confidenceLandCover;
            materCollection.ConfidenceCropType = confidenceCropType;
            materCollection.ConfidenceIrrigationType = confidenceIrrigationType;
            await _collectionMetadataRepository.UpdateAsync(materCollection);
            return new OkResult();
        }

        [HttpGet("collections/{collectionId}/metadata/download")]
        public async Task<IActionResult> DownloadMetadataExcel(string collectionId)
        {
            _logger.LogInformation($"Downloading metadata excel for -{collectionId}");
            var tenant = await GetTenant(collectionId);
            List<MetadataItem> items;
            using (CurrentTenant.Change(tenant.Id))
            {
                var collectionDefaultMetadata = await _collectionMetadataRepository.FirstAsync();
                var defaultItems = AddCollectionHelper.GetCollectionDefaultItems(collectionDefaultMetadata);
                var defaultNames = defaultItems.Select(x => x.Name);
                items = await (await _metadataItemsRepository.GetQueryableAsync())
                    .Where(x => !defaultNames.Contains(x.Name)).ToListAsync();
                items.AddRange(defaultItems);
                await InsertOrUpdateDb(defaultItems.ToArray(), tenant.Id);
            }

            var excelData = items.Select(x => new ExcelMetadata { Name = x.Name, Value = x.Value }).ToArray();
            byte[] bytes;
            using (var memStream = new MemoryStream { Position = 0 })
            {
                _logger.LogInformation($"Generating Excel metadata template for {collectionId}");
                ExcelOps.ExcelOps.WriteMetadata(memStream, excelData);
                bytes = await memStream.GetAllBytesAsync();
            }

            return new FileContentResult(bytes,
                "application/octet-stream") { FileDownloadName = $"{collectionId}_MetaDataUpload.xlsx" };
        }

        [Authorize]
        [HttpPut("collections/{collectionId}/metadata/calculate")]
        public async Task Recalculate(string collectionId, [FromQuery] bool date = false, [FromQuery] bool extent = false)
        {
            if (await _accessProvider.IsAdmin() == false)
            {
                throw new AbpException("Admin Auth required.");
            }

            _logger.LogInformation($"Downloading metadata excel for -{collectionId}");
            var tenant = await GetTenant(collectionId);
            CollectionMetadataEntity collectionDefaultMetadata;
            using (CurrentTenant.Change(tenant.Id))
            {
                collectionDefaultMetadata = await _collectionMetadataRepository.FirstAsync();
                if (await _itemRepository.AnyAsync() == false)
                {
                    return;
                }

                if (date == false && extent == false)
                {
                    collectionDefaultMetadata.FeatureCount = await _itemRepository.GetCountAsync();
                    collectionDefaultMetadata.EwocCodes = await (await _itemRepository.GetQueryableAsync())
                        .Select(x => x.EwocCode).Distinct().ToArrayAsync();
                    collectionDefaultMetadata.IrrTypes = await (await _itemRepository.GetQueryableAsync())
                        .Select(x => x.IrrigationStatus).Distinct().ToArrayAsync();
                }

                if (date)
                {
                    collectionDefaultMetadata.FirstDateOfValidityTime =
                        await (await _itemRepository.GetQueryableAsync())
                            .OrderBy(x => x.ValidityTime)
                            .Select(x => x.ValidityTime).FirstAsync();
                    collectionDefaultMetadata.LastDateOfValidityTime = await (await _itemRepository.GetQueryableAsync())
                        .OrderBy(x => x.ValidityTime)
                        .Select(x => x.ValidityTime).FirstAsync();
                }

                if (extent)
                {
                    var extentFromDb = await _addCollectionHelper.GetExtentFromDb(collectionId,
                        collectionDefaultMetadata.StoreType);
                    if (extentFromDb != null)
                    {
                        collectionDefaultMetadata.Extent = extentFromDb;
                    }
                }

                var defaultItems = AddCollectionHelper.GetCollectionDefaultItems(collectionDefaultMetadata);
                await InsertOrUpdateDb(defaultItems.ToArray(), tenant.Id);
            }

            // update master dataset metadata
            var materCollection =
                await _collectionMetadataRepository.FirstAsync(x => x.CollectionId == collectionId);
            materCollection.FeatureCount = collectionDefaultMetadata.FeatureCount;
            materCollection.EwocCodes = collectionDefaultMetadata.EwocCodes;
            materCollection.IrrTypes = collectionDefaultMetadata.IrrTypes;
            materCollection.FirstDateOfValidityTime = collectionDefaultMetadata.FirstDateOfValidityTime;
            materCollection.LastDateOfValidityTime = collectionDefaultMetadata.LastDateOfValidityTime;
            materCollection.Extent = collectionDefaultMetadata.Extent;
            await _collectionMetadataRepository.UpdateAsync(materCollection);
        }

        [Authorize]
        [HttpPut("collections/{collectionId}/metadata/item/{id:int}")]
        public async Task<MetadataItemDto> UpdateItem(string collectionId, int id, [FromBody] string value)
        {
            _logger.LogInformation($"Update metadata item for {collectionId}, {id}");
            var tenant = await GetTenant(collectionId);
            using (CurrentTenant.Change(tenant.Id))
            {
                var item = await _metadataItemsRepository.GetAsync(x => x.Id == id);
                item.Value = value;
                var updatedValue = await _metadataItemsRepository.UpdateAsync(item);
                return ObjectMapper.Map<MetadataItem, MetadataItemDto>(updatedValue);
            }
        }

        [Authorize]
        [HttpPut("collections/{collectionId}/metadata/{itemName}")]
        public async Task<MetadataItemDto> UpdateItemWithName(string collectionId, string itemName,
            [FromBody] string value)
        {
            _logger.LogInformation($"Update metadata item for {collectionId}, {itemName}");
            var tenant = await GetTenant(collectionId);
            using (CurrentTenant.Change(tenant.Id))
            {
                var item = await _metadataItemsRepository.GetAsync(x => x.Name == itemName);
                item.Value = value;
                var updatedValue = await _metadataItemsRepository.UpdateAsync(item);
                return ObjectMapper.Map<MetadataItem, MetadataItemDto>(updatedValue);
            }
        }

        [Authorize]
        [HttpDelete("collections/{collectionId}/metadata/item/{id:int}")]
        public async Task<IActionResult> Delete(string collectionId, int id)
        {
            if (await _accessProvider.IsAdmin() == false)
            {
                return new UnauthorizedObjectResult("Auth required.");
            }

            _logger.LogInformation($"Delete metadata item for {collectionId}, {id}");
            var tenant = await GetTenant(collectionId);
            using (CurrentTenant.Change(tenant.Id))
            {
                await _metadataItemsRepository.DeleteAsync(x => x.Id == id);
            }

            return new OkResult();
        }

        [HttpGet("collections/{collectionId}/events")]
        [Authorize]
        public async Task<DatasetEventViewModel[]> GetEvents(string collectionId)
        {
            _logger.LogInformation($"Getting events for -{collectionId}");
            var tenant = await GetTenant(collectionId);
            using (CurrentTenant.Change(tenant.Id))
            {
                var items = await (await _datasetEventRepository.GetQueryableAsync()).OrderBy(x => x.CreationTime)
                    .AsNoTracking().ToArrayAsync();
                var results = ObjectMapper.Map<DatasetEvent[], DatasetEventViewModel[]>(items);
                if (results.Any() && results.Last().Type == EventType.NeedsFix)
                {
                    results.Last().CanSubmit = true;
                }

                return results;
            }
        }

        [HttpGet("collections/{collectionId}/public/submit")]
        [Authorize]
        public async Task<IActionResult> Submit(string collectionId)
        {
            _logger.LogInformation($"Getting metadata for -{collectionId}");
            var tenant = await GetTenant(collectionId);
            using (CurrentTenant.Change(tenant.Id))
            {
                var items = await (await _datasetEventRepository.GetQueryableAsync()).OrderBy(x => x.CreationTime)
                    .AsNoTracking().ToArrayAsync();

                if (items.Any())
                {
                    if (items.Last().Type != EventType.NeedsFix)
                    {
                        return new BadRequestResult();
                    }

                    await _datasetEventRepository.InsertAsync(new DatasetEvent
                    {
                        Type = EventType.SubmittedForReview, TenantId = tenant.Id,
                        Comments = new[] { "Resubmitted for check." }
                    });
                }
                else
                {
                    await _datasetEventRepository.InsertAsync(new DatasetEvent
                    {
                        Type = EventType.SubmittedForReview, TenantId = tenant.Id,
                        Comments = new[] { "Submitted for making dataset public." }
                    });
                }
            }

            return new OkResult();
        }

        [HttpPut("collections/{collectionId}/add/{eventType}")]
        [Authorize]
        public async Task<IActionResult> AddEvent(string collectionId, EventType eventType,
            [FromBody] string[] comments)
        {
            _logger.LogInformation($"Getting metadata for -{collectionId}");
            var tenant = await GetTenant(collectionId);
            using (CurrentTenant.Change(tenant.Id))
            {
                await _datasetEventRepository.InsertAsync(new DatasetEvent
                    { Type = eventType, TenantId = tenant.Id, Comments = comments });
            }

            return new OkResult();
        }

        private async Task InsertOrUpdateDb(MetadataItem[] metadatas, Guid tenantId)
        {
            _logger.LogInformation($"InsertOrUpdateDb metadata item for tenant-{tenantId}");
            var entityListToInsert = new List<MetadataItem>();
            var idsToDelete = new List<int>();

            foreach (var item in metadatas)
            {
                if (await _metadataItemsRepository.AnyAsync(x => x.Name == item.Name))
                {
                    if (ExcelMarkers.AttributesFromDatasetNoUpdate.Contains(item.Name))
                    {
                        continue; // dataset info from excel data not be updated.
                    }

                    var matchIdsToDelete
                        = await (await _metadataItemsRepository.GetQueryableAsync()).Where(x => x.Name == item.Name)
                            .Select(x => x.Id)
                            .ToListAsync();
                    idsToDelete.AddRange(matchIdsToDelete);
                }

                var entity = new MetadataItem { Name = item.Name, Value = item.Value, TenantId = tenantId };
                entityListToInsert.Add(entity);
            }

            await _metadataItemsRepository.DeleteManyAsync(idsToDelete);
            await _metadataItemsRepository.InsertManyAsync(entityListToInsert);
        }

        private async Task<Tenant> GetTenant(string collectionId)
        {
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