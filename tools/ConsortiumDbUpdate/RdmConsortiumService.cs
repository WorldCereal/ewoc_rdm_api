using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aws4RequestSigner;
using Flurl.Http;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.Enums;
using IIASA.WorldCereal.Rdm.ExcelOps;
using IIASA.WorldCereal.Rdm.Jobs.UploadUserDataset;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql.Bulk;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace ConsortiumDbUpdate
{
    [UnitOfWork]
    public class RdmConsortiumService : ITransientDependency, IUnitOfWorkEnabled
    {
        private readonly ILogger<RdmConsortiumService> _logger;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly IAddCollectionHelper _addCollectionHelper;
        private readonly ITenantRepository _tenantRepository;
        private readonly IConfiguration _configuration;
        private readonly ICurrentTenant _currentTenant;
        private readonly IRepository<ItemEntity, long> _itemRepository;
        private readonly IRepository<MetadataItem, int> _metadataRepository;
        private readonly IRepository<CollectionMetadataEntity, Guid> _tenantCollectionStoreRepository;
        private readonly IRepository<ConsortiumCollection, int> _consortiumColsRepository;
        private IUnitOfWork _unitOfWork;
        private readonly S3Config _s3Config;

        public RdmConsortiumService(ILogger<RdmConsortiumService> logger, IUnitOfWorkManager unitOfWorkManager,
            IAddCollectionHelper addCollectionHelper, ITenantRepository tenantRepository, IConfiguration configuration,
            ICurrentTenant currentTenant, IRepository<ItemEntity, long> itemRepository,
            IRepository<MetadataItem, int> metadataRepository,
            IRepository<CollectionMetadataEntity, Guid> tenantCollectionStoreRepository,
            IRepository<ConsortiumCollection, int> consortiumColsRepository)
        {
            _logger = logger;
            _unitOfWorkManager = unitOfWorkManager;
            _addCollectionHelper = addCollectionHelper;
            _tenantRepository = tenantRepository;
            _configuration = configuration;
            _currentTenant = currentTenant;
            _itemRepository = itemRepository;
            _metadataRepository = metadataRepository;
            _tenantCollectionStoreRepository = tenantCollectionStoreRepository;
            _consortiumColsRepository = consortiumColsRepository;

            _s3Config = new S3Config();
            _configuration.Bind("S3Config", _s3Config);
        }

        [UnitOfWork]
        public async Task Start()
        {
            _unitOfWork = _unitOfWorkManager.Current;
            if (_unitOfWork == null)
            {
                _unitOfWork = _unitOfWorkManager.Begin(requiresNew: true);
            }

            var consortiumCollections =
                await _consortiumColsRepository.Where(x => x.NeedsUpdate).AsNoTracking().ToListAsync();

            _logger.LogInformation($"Found following {consortiumCollections.Count} items for update");

            foreach (var item in consortiumCollections)
            {
                _logger.LogInformation(
                    $"{item.Id}.NeedsUpdate={item.NeedsUpdate},OverwriteFeatures={item.OverwriteFeatures}," +
                    $"OverwriteOtherProps={item.OverwriteOtherProps},{item.DownloadZipUrl}");
            }

            if (consortiumCollections.Any() == false)
            {
                _logger.LogInformation("No collections to update");
                return;
            }

            FlurlHttp.GlobalSettings.Timeout = TimeSpan.FromMinutes(5);
            FlurlHttp.GlobalSettings.ConnectionLeaseTimeout = TimeSpan.FromMinutes(5);
            var tempPath = _configuration.GetValue<string>("TempFolder");


            foreach (var collection in consortiumCollections)
            {
                if (collection.NeedsUpdate == false)
                {
                    continue;
                }

                try
                {
                    collection.Successful = false;
                    collection.Errors = string.Empty;

                    await AddOrUpdateCollection(collection, tempPath);
                    collection.Successful = true;
                    collection.NeedsUpdate = false;
                    collection.OverwriteFeatures = false;
                    collection.OverwriteOtherProps = false;
                    _logger.LogInformation(
                        $"Completed! - {collection.Id}.NeedsUpdate={collection.NeedsUpdate}," +
                        $"OverwriteFeatures={collection.OverwriteFeatures}," +
                        $"OverwriteOtherProps={collection.OverwriteOtherProps}," +
                        $"{collection.DownloadZipUrl}");
                }
                catch (Exception e)
                {
                    collection.Errors = e.ToString();
                }
            }

            await _consortiumColsRepository.UpdateManyAsync(consortiumCollections);
            await _unitOfWork.SaveChangesAsync(); // save changes to db
        }

        private async Task AddOrUpdateCollection(ConsortiumCollection collection, string tempPath)
        {
            var filesFolder = await DownloadAndGetFiles(collection, tempPath);
            var metadataFile = GetFileWithExt(filesFolder, "*.xlsx");
            _logger.LogInformation($"Extracting metadata from -{metadataFile}");
            var excelMetadatas = GetExcelMetadatas(metadataFile);

            //AddDownloadUrlToMetadata(excelMetadatas, collection.DownloadZipUrl); Will add later.

            var collectionId = GetValue(excelMetadatas, ExcelMarkers.DatasetCollectionId).ToLowerInvariant()
                .Replace("_", string.Empty).Replace("-", string.Empty).Trim();

            var filePath = GetFileWithExt(filesFolder, "*.shp");
            if (filePath == default)
            {
                filePath = GetFileWithExt(filesFolder, "*.gpkg");
            }

            var geometryFactory = new GeometryFactory(new PrecisionModel(), GeoJsonHelper.GeometryWgs84Srid);
            _logger.LogInformation($"Extracting Features from -{filePath}");
            var typeShape = ProvisionUserDataSetJob.GetShapeGeometryType(geometryFactory, out var bounds,
                out var items, collectionId, filePath);
            Directory.Delete(filesFolder, true);
            var tenant = await _tenantRepository.FindByNameAsync(collectionId);
            if (tenant == null)
            {
                await Add(geometryFactory, bounds, items, typeShape, excelMetadatas, collectionId);
            }
            else
            {
                MetadataItem[] metadataItems = excelMetadatas.Select(x =>
                    new MetadataItem {Name = x.Name, Value = x.Value, TenantId = tenant.Id}).ToArray();
                if (collection.OverwriteFeatures)
                {
                    var masterCollectionStoreInfo = await UpdateMasterCollectionStoreInfo(collectionId, geometryFactory,
                        bounds, items, typeShape, excelMetadatas);
                    using (_currentTenant.Change(tenant.Id))
                    {
                        await _itemRepository.DeleteAsync(x => true, true);
                        await _metadataRepository.DeleteAsync(x => true, true);
                        await _tenantCollectionStoreRepository.DeleteAsync(x => true, true);
                        await _unitOfWork.SaveChangesAsync();
                    }

                    await _unitOfWork.SaveChangesAsync();
                    _logger.LogInformation($"Adding {collectionId} to tenant store");
                    await AddFeatures(tenant, masterCollectionStoreInfo, items, metadataItems);
                }
                else if (collection.OverwriteOtherProps)
                {
                    using (_currentTenant.Change(tenant.Id))
                    {
                        await _metadataRepository.DeleteAsync(x => true, true);
                        await _unitOfWork.SaveChangesAsync();
                    }

                    var updatedCol = await UpdateMasterCollectionStoreInfo(collectionId, geometryFactory,
                        bounds, items, typeShape, excelMetadatas);
                    var context = AddUserDatasetItemsJob.GetTenantContext(tenant.ConnectionStrings.First().Value);
                    NpgsqlBulkUploader uploader = new NpgsqlBulkUploader(context);
                    _logger.LogInformation($"Adding {metadataItems.Length} metadataItems to tenant");
                    await uploader.ImportAsync(metadataItems);

                    updatedCol.TenantId = tenant.Id;
                    await uploader.ImportAsync(new[] {updatedCol});
                    updatedCol.TenantId = null;
                }
                else
                {
                    await AddFeatures(tenant, items); // partial update of dataset.

                    // update feature counts
                    using (_currentTenant.Change(tenant.Id))
                    {
                        var data = await _tenantCollectionStoreRepository.FirstAsync();
                        data.FeatureCount += items.Count;
                        await _unitOfWork.SaveChangesAsync();
                    }

                    var dbentity = await _addCollectionHelper.GetMasterCollectionStoreInfo(collectionId);
                    dbentity.FeatureCount += items.Count;
                    await _addCollectionHelper.SaveMasterCollectionStoreInfoAsync(dbentity);
                }
            }
        }

        private void AddDownloadUrlToMetadata(List<ExcelMetadata> excelMetadatas, string collectionDownloadZipUrl)
        {
            excelMetadatas.Add(new ExcelMetadata
            {
                Name = ExcelMarkers.CollectionDownloadUrl,
                Value = collectionDownloadZipUrl
            });
        }

        private async Task<CollectionMetadataEntity> UpdateMasterCollectionStoreInfo(string collectionId,
            GeometryFactory geometryFactory,
            Envelope bounds, IList<ItemEntity> items, ShapeGeometryType typeShape, List<ExcelMetadata> excelMetadatas)
        {
            var dbentity = await _addCollectionHelper.GetMasterCollectionStoreInfo(collectionId);
            var masterCollectionStoreInfo =
                ProvisionUserDataSetJob.UpdateMasterCollectionEntity(dbentity, geometryFactory, bounds, items,
                    typeShape,
                    GetValue(excelMetadatas, ExcelMarkers.DatasetTitle),
                    TypeOfObsMethodHelper.Get(GetValue(excelMetadatas, ExcelMarkers.TypeOfObservationMethod)),
                    collectionId);
            UpdateConfScoresAndAccess(masterCollectionStoreInfo, excelMetadatas);
            await _addCollectionHelper.SaveMasterCollectionStoreInfoAsync(masterCollectionStoreInfo);
            return masterCollectionStoreInfo;
        }

        private async Task Add(GeometryFactory geometryFactory, Envelope bounds, IList<ItemEntity> items,
            ShapeGeometryType typeShape,
            List<ExcelMetadata> excelMetadatas, string collectionId)
        {
            var insertedCollectionMetadataEntity =
                await Create(geometryFactory, bounds, items, typeShape, excelMetadatas, collectionId);
            await _unitOfWork.SaveChangesAsync(); // save changes to db
            _logger.LogInformation($"Completed adding {collectionId} to master store");

            // now update the tenant
            var tenant = await _tenantRepository.FindByNameAsync(collectionId);
            MetadataItem[] metadataItems = excelMetadatas.Select(x =>
                new MetadataItem {Name = x.Name, Value = x.Value, TenantId = tenant.Id}).ToArray();
            _logger.LogInformation($"Adding {collectionId} to tenant store");
            await AddFeatures(tenant, insertedCollectionMetadataEntity, items, metadataItems);
        }

        private async Task<CollectionMetadataEntity> Create(GeometryFactory geometryFactory, Envelope bounds,
            IList<ItemEntity> items, ShapeGeometryType typeShape,
            List<ExcelMetadata> excelMetadatas, string collectionId)
        {
            var masterCollectionStoreInfo =
                ProvisionUserDataSetJob.GetMasterCollectionEntity(geometryFactory, bounds, items, typeShape,
                    GetValue(excelMetadatas, ExcelMarkers.DatasetTitle),
                    TypeOfObsMethodHelper.Get(GetValue(excelMetadatas, ExcelMarkers.TypeOfObservationMethod)),
                    collectionId);
            _logger.LogInformation($"Creating database and other details-{collectionId}");
            UpdateConfScoresAndAccess(masterCollectionStoreInfo, excelMetadatas);
            var insertedCollectionMetadataEntity =
                await _addCollectionHelper.CreateMasterCollectionStoreInfo(masterCollectionStoreInfo);
            return insertedCollectionMetadataEntity;
        }

        private void UpdateConfScoresAndAccess(CollectionMetadataEntity entity, List<ExcelMetadata> excelMetadatas)
        {
            entity.ConfidenceLandCover = ConfHelper.Get(GetValue(excelMetadatas, ExcelMarkers.ConfidenceLandCover));
            entity.ConfidenceCropType = ConfHelper.Get(GetValue(excelMetadatas, ExcelMarkers.ConfidenceCropType));
            entity.ConfidenceIrrigationType =
                ConfHelper.Get(GetValue(excelMetadatas, ExcelMarkers.ConfidenceIrrigation));
            entity.AccessType =
                GetValue(excelMetadatas, ExcelMarkers.TypeOfLicenseAccessType).ToLowerInvariant() == "private"
                    ? AccessType.Private
                    : AccessType.Public;
            entity.StoreType = StoreType.Reference;
        }

        private async Task AddFeatures(Tenant tenant, CollectionMetadataEntity insertedCollectionMetadataEntity,
            IList<ItemEntity> items, MetadataItem[] metadataItems)
        {
            var context = AddUserDatasetItemsJob.GetTenantContext(tenant.ConnectionStrings.First().Value);
            NpgsqlBulkUploader uploader = new NpgsqlBulkUploader(context);
            insertedCollectionMetadataEntity.TenantId = tenant.Id;
            foreach (var entity in items)
            {
                entity.TenantId = tenant.Id;
            }

            await uploader.ImportAsync(new[] {insertedCollectionMetadataEntity});
            insertedCollectionMetadataEntity.TenantId = null;
            await Import(items, uploader);

            _logger.LogInformation($"Adding {metadataItems.Length} metadataItems to tenant");
            await uploader.ImportAsync(metadataItems);
        }

        private async Task AddFeatures(Tenant tenant, IList<ItemEntity> items)
        {
            var context = AddUserDatasetItemsJob.GetTenantContext(tenant.ConnectionStrings.First().Value);
            NpgsqlBulkUploader uploader = new NpgsqlBulkUploader(context);
            foreach (var entity in items)
            {
                entity.TenantId = tenant.Id;
            }

            await Import(items, uploader);
        }

        private async Task Import(IList<ItemEntity> items, NpgsqlBulkUploader uploader)
        {
            var itemsCount = items.Count;
            _logger.LogInformation($"Adding {itemsCount} features to tenant");
            var batchCount = 5000;
            for (int index = 0; index < itemsCount; index += batchCount)
            {
                var batch = items.Skip(index).Take(batchCount).ToArray();
                _logger.LogInformation($"Adding {batch.Length} batch features to tenant. Skip index:{index}");
                await uploader.ImportAsync(batch);
            }
        }

        private List<ExcelMetadata> GetExcelMetadatas(string metadataFile)
        {
            List<ExcelMetadata> excelMetadatas;
            using (Stream stream = File.OpenRead(metadataFile))
            {
                excelMetadatas = ExcelOps.ExtractCollectionMetadata(stream);
            }

            foreach (var metadata in excelMetadatas)
            {
                _logger.LogInformation($"metadata.Name-{metadata.Name},metadata.Value-{metadata.Value}");
            }

            return excelMetadatas;
        }

        private static string GetFileWithExt(string filesFolder, string searchPattern)
        {
            return SearchHelper.GetFiles(filesFolder, searchPattern).FirstOrDefault();
        }

        private static string GetValue(List<ExcelMetadata> excelMetadatas, string parameter)
        {
            return excelMetadatas.First(x => x.Name == parameter).Value;
        }

        private async Task<string> DownloadAndGetFiles(ConsortiumCollection collection, string tempPath)
        {
            _logger.LogInformation($"Downloading file from-{collection.DownloadZipUrl}");
            var file = new FileInfo(collection.DownloadZipUrl);
            var zipFilePath = Path.Combine(tempPath, file.Name);
            if (File.Exists(zipFilePath))
            {
                File.Delete(zipFilePath);
            }

            await DownloadFromUrl(zipFilePath, collection.DownloadZipUrl);

            var filesFolder = Path.Combine(tempPath, Path.GetFileNameWithoutExtension(file.Name));
            if (Directory.Exists(filesFolder))
            {
                Directory.Delete(filesFolder, true);
            }

            _logger.LogInformation($"Unzipping file- {zipFilePath}");
            ZipFile.ExtractToDirectory(zipFilePath, filesFolder, Encoding.UTF8, true);
            File.Delete(zipFilePath);
            return filesFolder;
        }

        private async Task DownloadFromUrl(string zipFilePath, string downloadUrl)
        {
            var signer = new AWS4RequestSigner(_s3Config.AccessKey, _s3Config.Secret);
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(downloadUrl)
            };

            request = await signer.Sign(request, _s3Config.ServiceName, _s3Config.Region);

            int bufferSize = 4096;
            var client = new HttpClient();
            using (var response = await client.SendAsync(request))
            {
                using (var httpStream = await response.Content.ReadAsStreamAsync())
                {
                    using (var fileStream = new FileStream(zipFilePath, FileMode.Create, FileAccess.Write,
                        FileShare.None,
                        bufferSize, true))
                    {
                        await httpStream.CopyToAsync(fileStream, bufferSize, new CancellationToken());
                    }
                }
            }
        }
    }

    public class S3Config
    {
        public string AccessKey { get; set; }
        public string Secret { get; set; }
        public string Region { get; set; }
        public string ServiceName { get; set; }
    }
}