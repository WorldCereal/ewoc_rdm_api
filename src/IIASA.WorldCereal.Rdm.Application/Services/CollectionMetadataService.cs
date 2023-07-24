using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Dtos;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.Enums;
using IIASA.WorldCereal.Rdm.ExcelOps;
using IIASA.WorldCereal.Rdm.Mappings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
        private readonly IRepository<CollectionMetadataEntity> _collectionMetadataRepository;
        private readonly ILogger<CollectionMetadataService> _logger;
        private readonly IEwocUser _ewocUser;
        private readonly ITenantRepository _tenantRepository;

        public CollectionMetadataService(IRepository<MetadataItem, int> metadataItemsRepository,
            IRepository<DatasetEvent, int> datasetEventRepository,
            IRepository<CollectionMetadataEntity> collectionMetadataRepository,
            ILogger<CollectionMetadataService> logger, IEwocUser ewocUser,
            ITenantRepository tenantRepository)
        {
            _metadataItemsRepository = metadataItemsRepository;
            _datasetEventRepository = datasetEventRepository;
            _collectionMetadataRepository = collectionMetadataRepository;
            _logger = logger;
            _ewocUser = ewocUser;
            _tenantRepository = tenantRepository;
        }

        [HttpGet("collections/{collectionId}/metadata/items")]
        public async Task<MetadataItemDto[]> GetMetadata(string collectionId)
        {
            if (_ewocUser.IsAuthenticated == false && _ewocUser.IsAdmin == false)
            {
                throw new UnauthorizedAccessException("Auth required.");
            }

            _logger.LogInformation($"Getting metadata for -{collectionId}");
            var tenant = await GetTenant(collectionId);
            using (CurrentTenant.Change(tenant.Id))
            {
                var items = await _metadataItemsRepository.Where(x => true).AsNoTracking().ToArrayAsync();
                return ObjectMapper.Map<MetadataItem[], MetadataItemDto[]>(items);
            }
        }

        [HttpPost("collections/{collectionId}/metadata/item")]
        public async Task<MetadataItemDto> CreateMetadataItem(string collectionId,
            [FromBody] MetadataItemDto metadataItemDto)
        {
            if (_ewocUser.CanAccessUserData == false)
            {
                throw new UnauthorizedAccessException("Auth required.");
            }

            _logger.LogInformation($"Creating metadata item for -{collectionId}");
            var tenant = await GetTenant(collectionId);
            using (CurrentTenant.Change(tenant.Id))
            {
                metadataItemDto.Id = 0;
                var entity = ObjectMapper.Map<MetadataItemDto, MetadataItem>(metadataItemDto);
                entity.TenantId = tenant.Id;
                var newEntity = await _metadataItemsRepository.InsertAsync(entity);
                return ObjectMapper.Map<MetadataItem, MetadataItemDto>(newEntity);
            }
        }

        [HttpPost("collections/{collectionId}/metadata/upload")]
        public async Task<IActionResult> UploadMetadataExcel(string collectionId, IFormFile excelBook)
        {
            if (_ewocUser.CanAccessUserData == false)
            {
                return new UnauthorizedObjectResult("Auth required.");
            }

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
            double confidenceLandCover =
                double.Parse(metadataItems.First(x => x.Name == ExcelMarkers.ConfidenceLandCover).Value);
            double confidenceCropType =
                double.Parse(metadataItems.First(x => x.Name == ExcelMarkers.ConfidenceCropType).Value);
            double confidenceIrrigationType =
                double.Parse(metadataItems.First(x => x.Name == ExcelMarkers.ConfidenceIrrigation).Value);
            var accessType = excelMetadatas.First(x => x.Name == ExcelMarkers.TypeOfLicenseAccessType).Value
                .ToLowerInvariant() == "private"? AccessType.Private : AccessType.Public;

            // update tenant dataset metadata
            using (CurrentTenant.Change(tenant.Id))
            {
                await InsertOrUpdateDb(metadataItems, tenant.Id);

                CollectionMetadataEntity collectionDefaultMetadata = await _collectionMetadataRepository.FirstAsync();
                collectionDefaultMetadata.Title = title;
                collectionDefaultMetadata.ConfidenceLandCover = confidenceLandCover;
                collectionDefaultMetadata.ConfidenceCropType = confidenceCropType;
                collectionDefaultMetadata.ConfidenceIrrigationType = confidenceIrrigationType;
                collectionDefaultMetadata.AccessType = accessType;
                await _collectionMetadataRepository.UpdateAsync(collectionDefaultMetadata);
            }

            // update master dataset metadata
            CollectionMetadataEntity materCollection =
                await _collectionMetadataRepository.FirstAsync(x => x.CollectionId == collectionId);
            materCollection.Title = title;
            materCollection.ConfidenceLandCover = confidenceLandCover;
            materCollection.ConfidenceCropType = confidenceCropType;
            materCollection.ConfidenceIrrigationType = confidenceIrrigationType;
            materCollection.AccessType = accessType;
            await _collectionMetadataRepository.UpdateAsync(materCollection);
            return new OkResult();
        }

        [HttpGet("collections/{collectionId}/metadata/download")]
        public async Task<IActionResult> DownloadMetadataExcel(string collectionId)
        {
            if (_ewocUser.CanAccessUserData == false)
            {
                return new UnauthorizedObjectResult("Auth required.");
            }

            _logger.LogInformation($"Downloading metadata excel for -{collectionId}");
            var tenant = await GetTenant(collectionId);
            List<MetadataItem> items;
            using (CurrentTenant.Change(tenant.Id))
            {
                var collectionDefaultMetadata = await _collectionMetadataRepository.FirstAsync();
                var defaultItems = GetCollectionDefaultItems(collectionDefaultMetadata);
                var defaultNames = defaultItems.Select(x => x.Name);
                items = await _metadataItemsRepository.Where(x => !defaultNames.Contains(x.Name)).ToListAsync();
                items.AddRange(defaultItems);
                await InsertOrUpdateDb(defaultItems.ToArray(), tenant.Id);
            }

            var excelData = items.Select(x => new ExcelMetadata {Name = x.Name, Value = x.Value}).ToArray();
            byte[] bytes;
            using (var memStream = new MemoryStream {Position = 0})
            {
                _logger.LogInformation($"Generating Excel metadata template for {collectionId}");
                ExcelOps.ExcelOps.WriteMetadata(memStream, excelData);
                bytes = await memStream.GetAllBytesAsync();
            }

            return new FileContentResult(bytes,
                "application/octet-stream") {FileDownloadName = $"{collectionId}_MetaDataUpload.xlsx"};
        }

        [HttpPost("collections/{collectionId}/metadata/items")]
        public async Task CreateManyMetadataItem(string collectionId, [FromBody] MetadataItemDto[] metadataItemDtos)
        {
            if (_ewocUser.CanAccessUserData == false)
            {
                throw new UnauthorizedAccessException("Auth required.");
            }

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
        }

        [HttpPut("collections/{collectionId}/metadata/item/{id:int}")]
        public async Task<MetadataItemDto> UpdateItem(string collectionId, int id, [FromBody] string value)
        {
            if (_ewocUser.CanAccessUserData == false)
            {
                throw new UnauthorizedAccessException("Auth required.");
            }

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

        [HttpDelete("collections/{collectionId}/metadata/item/{id:int}")]
        public async Task<IActionResult> Delete(string collectionId, int id)
        {
            if (_ewocUser.CanAccessUserData == false)
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
        public async Task<DatasetEventViewModel[]> GetEvents(string collectionId)
        {
            if (_ewocUser.CanAccessUserData == false)
            {
                throw new UnauthorizedAccessException("Auth required.");
            }

            _logger.LogInformation($"Getting events for -{collectionId}");
            var tenant = await GetTenant(collectionId);
            using (CurrentTenant.Change(tenant.Id))
            {
                var items = await _datasetEventRepository.OrderBy(x => x.CreationTime).AsNoTracking().ToArrayAsync();
                var results = ObjectMapper.Map<DatasetEvent[], DatasetEventViewModel[]>(items);
                if (results.Any() && results.Last().Type == EventType.NeedsFix)
                {
                    results.Last().CanSubmit = true;
                }

                return results;
            }
        }

        [HttpGet("collections/{collectionId}/public/submit")]
        public async Task<IActionResult> Submit(string collectionId)
        {
            if (_ewocUser.CanAccessUserData == false)
            {
                return new UnauthorizedObjectResult("Auth required.");
            }

            _logger.LogInformation($"Getting metadata for -{collectionId}");
            var tenant = await GetTenant(collectionId);
            using (CurrentTenant.Change(tenant.Id))
            {
                var items = await _datasetEventRepository.OrderBy(x => x.CreationTime)
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
                        Comments = new[] {"Resubmitted for check."}
                    });
                }
                else
                {
                    await _datasetEventRepository.InsertAsync(new DatasetEvent
                    {
                        Type = EventType.SubmittedForReview, TenantId = tenant.Id,
                        Comments = new[] {"Submitted for making dataset public."}
                    });
                }
            }

            return new OkResult();
        }

        [HttpPut("collections/{collectionId}/add/{eventType}")]
        public async Task<IActionResult> AddEvent(string collectionId, EventType eventType,
            [FromBody] string[] comments)
        {
            if (_ewocUser.IsAdmin == false)
            {
                return new UnauthorizedObjectResult("Admin Auth required.");
            }

            _logger.LogInformation($"Getting metadata for -{collectionId}");
            var tenant = await GetTenant(collectionId);
            using (CurrentTenant.Change(tenant.Id))
            {
                await _datasetEventRepository.InsertAsync(new DatasetEvent
                    {Type = eventType, TenantId = tenant.Id, Comments = comments});
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
                if (_metadataItemsRepository.Any(x => x.Name == item.Name))
                {
                    if (ExcelMarkers.AttributesFromDatasetNoUpdate.Contains(item.Name))
                    {
                        continue; // dataset info from excel data not be updated.
                    }

                    var matchIdsToDelete
                        = await _metadataItemsRepository.Where(x => x.Name == item.Name).Select(x => x.Id)
                            .ToListAsync();
                    idsToDelete.AddRange(matchIdsToDelete);
                }

                var entity = new MetadataItem {Name = item.Name, Value = item.Value, TenantId = tenantId};
                entityListToInsert.Add(entity);
            }

            await _metadataItemsRepository.DeleteManyAsync(idsToDelete);
            await _metadataItemsRepository.InsertManyAsync(entityListToInsert);
        }

        private IList<MetadataItem> GetCollectionDefaultItems(CollectionMetadataEntity data)
        {
            return new List<MetadataItem>
            {
                new() {Name = ExcelMarkers.DatasetCollectionId, Value = data.CollectionId},
                new() {Name = ExcelMarkers.DatasetTitle, Value = data.Title},
                new() {Name = ExcelMarkers.ConfidenceLandCover, Value = data.ConfidenceLandCover.ToString("F")},
                new() {Name = ExcelMarkers.ConfidenceCropType, Value = data.ConfidenceCropType.ToString("F")},
                new() {Name = ExcelMarkers.ConfidenceIrrigation, Value = data.ConfidenceIrrigationType.ToString("F")},
                new()
                {
                    Name = ExcelMarkers.GeometryPointOrPolygonOrRaster,
                    Value = GetDsType(data)
                },
                new()
                {
                    Name = ExcelMarkers.TypeOfObservationMethod,
                    Value = data.TypeOfObservationMethod.ToString("G")
                },
                new()
                {
                    Name = ExcelMarkers.NoOfObservations,
                    Value = data.FeatureCount.ToString()
                },
                new()
                {
                    Name = ExcelMarkers.ListLandCovers,
                    Value = string.Join(";", data.LandCovers)
                },
                new()
                {
                    Name = ExcelMarkers.ListOfCropTypes,
                    Value = string.Join(";", data.CropTypes)
                },
                new()
                {
                    Name = ExcelMarkers.ListOfIrrigationCodes,
                    Value = string.Join(";", data.IrrTypes)
                },
                new()
                {
                    Name = ExcelMarkers.GeometryBoundingBoxLl,
                    Value = SpatialMappingHelper.GetLowerLeftCoordinates(data.Extent)
                },
                new()
                {
                    Name = ExcelMarkers.GeometryBoundingBoxUr,
                    Value = SpatialMappingHelper.GetUpperRightCoordinates(data.Extent)
                },
                new()
                {
                    Name = ExcelMarkers.FirstDateObservation,
                    Value = data.FirstDateOfValidityTime.ToString("d/M/yyyy")
                },
                new()
                {
                    Name = ExcelMarkers.LastDateObservation,
                    Value = data.LastDateOfValidityTime.ToString("d/M/yyyy")
                },
            };
        }

        private static string GetDsType(CollectionMetadataEntity data)
        {
            if (data.Type == CollectionType.ClassifiedMap)
            {
                return "Raster";
            }

            if (data.Type == CollectionType.Point)
            {
                return "Point";
            }

            return "Polygon";
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