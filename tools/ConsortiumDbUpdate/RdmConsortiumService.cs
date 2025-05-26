using System.IO.Compression;
using System.Text;
using EFCore.BulkExtensions;
using IIASA.WorldCereal.Rdm;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Core.Vector;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.EntityFrameworkCore;
using IIASA.WorldCereal.Rdm.Enums;
using IIASA.WorldCereal.Rdm.ExcelOps;
using IIASA.WorldCereal.Rdm.Jobs.UploadUserDataset;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
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
        private readonly IItemsCodeStatsHelper _itemsCodeStatsHelper;
        private readonly IRepository<CollectionMetadataEntity, Guid> _tenantCollectionStoreRepository;
        private readonly IRepository<ConsortiumCollection, int> _consortiumColsRepository;
        private IUnitOfWork? _unitOfWork;
        private AzureFileUploader _azureFileUploader;

        public RdmConsortiumService(ILogger<RdmConsortiumService> logger, IUnitOfWorkManager unitOfWorkManager,
            IAddCollectionHelper addCollectionHelper, ITenantRepository tenantRepository, IConfiguration configuration,
            ICurrentTenant currentTenant, IRepository<ItemEntity, long> itemRepository,
            IRepository<MetadataItem, int> metadataRepository, IItemsCodeStatsHelper itemsCodeStatsHelper,
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
            _itemsCodeStatsHelper = itemsCodeStatsHelper;
            _tenantCollectionStoreRepository = tenantCollectionStoreRepository;
            _consortiumColsRepository = consortiumColsRepository;

            var azureConfig = new AzureConfig();
            _configuration.GetSection("AzureStoreConfig").Bind(azureConfig);
            _azureFileUploader = new AzureFileUploader(azureConfig);
        }
        
        public async Task Start()
        {
            var result = true;
            var cycle = 1;
            while (result)
            {
                Console.WriteLine($"Cycle - {cycle++}");
                result = await Process();
            }
        }

        [UnitOfWork]
        private async Task<bool> Process()
        {
            _unitOfWork = _unitOfWorkManager.Current;
            if (_unitOfWork == null)
            {
                _unitOfWork = _unitOfWorkManager.Begin(requiresNew: true);
            }

            var consortiumCollections = await (await _consortiumColsRepository.GetQueryableAsync())
                .Where(x => x.NeedsUpdate).AsNoTracking()
                .ToListAsync();

            Console.WriteLine($"Found following {consortiumCollections.Count} items for update");

            foreach (var item in consortiumCollections)
            {
                Console.WriteLine(
                    $"{item.Id}.NeedsUpdate={item.NeedsUpdate},OverwriteFeatures={item.OverwriteFeatures}," +
                    $"OverwriteOtherProps={item.OverwriteOtherProps},{item.DownloadZipUrl}");
            }

            if (consortiumCollections.Any() == false)
            {
                Console.WriteLine("No collections to update");
                return false;
            }


            //FlurlHttp.GlobalSettings.Timeout = TimeSpan.FromMinutes(5);
            //FlurlHttp.GlobalSettings.ConnectionLeaseTimeout = TimeSpan.FromMinutes(5);
            var tempPath = _configuration.GetValue<string>("TempFolder");

            foreach (var collection in consortiumCollections.Take(2))
            {
                if (collection.NeedsUpdate == false)
                {
                    continue;
                }

                try
                {
                    collection.Successful = false;
                    collection.Errors = string.Empty;

                    Console.WriteLine($"Deleting all files in path - {tempPath}");
                    var files = Directory.GetFiles(tempPath, "*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        try
                        {
                            // Delete the file
                            File.Delete(file);
                            Console.WriteLine($"Deleted: {file}");
                        }
                        catch (Exception ex)
                        {
                            // If an error occurs (e.g., file is in use, no permissions), show an error message
                            Console.WriteLine($"Failed to delete {file}: {ex.Message}");
                        }
                    }

                    await AddOrUpdateCollection(collection, tempPath);
                    collection.Successful = true;
                    collection.NeedsUpdate = false;
                    collection.OverwriteFeatures = false;
                    collection.OverwriteOtherProps = false;
                    Console.WriteLine(
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
            return consortiumCollections.Any(x=>x.NeedsUpdate);
        }

        private async Task AddOrUpdateCollection(ConsortiumCollection collection, string tempPath)
        {
            Console.WriteLine($"Starting to add Dataset from file-{collection.DownloadZipUrl}");
            var filesFolder = await DownloadAndGetFiles(collection, tempPath);
            var metadataFile = GetFileWithExt(filesFolder, "*.xlsx");
            Console.WriteLine($"Extracting metadata from -{metadataFile}");
            var excelMetadatas = GetExcelMetadatas(metadataFile);

            var collectionId = GetValue(excelMetadatas, ExcelMarkers.DatasetCollectionId).ToLowerInvariant()
                .Replace(" ", "").Replace("-", "").Trim();

            var datasetGeoParquetFilePath = GetFileWithExt(filesFolder, "*.geoparquet|*.parquet");

            var harmonizationPdfFilePath = GetFileWithExt(filesFolder, "*.pdf");

            var harmonizationXlFilePath = GetFileWithExt(filesFolder, "*.xlsx");

            var geometryFactory = new GeometryFactory(new PrecisionModel(), GeoJsonHelper.GeometryWgs84Srid);
            Console.WriteLine($"Extracting Features from -{datasetGeoParquetFilePath}");
            var typeShape = ProvisionUserDataSetJob.GetShapeGeometryType(geometryFactory, out var bounds,
                out var items, collectionId, datasetGeoParquetFilePath);

            UpdateItemsWithConfScores(items, excelMetadatas);

            var sampleFilePath = await GenerateAndSaveSamples(items, bounds, filesFolder,
                string.Concat("sample_", new FileInfo(datasetGeoParquetFilePath).Name.Split(".")[0]));

            await AddDownloadUrl(excelMetadatas, datasetGeoParquetFilePath, sampleFilePath, harmonizationPdfFilePath,
                harmonizationXlFilePath,
                collectionId, filesFolder);

            var tenant = await _tenantRepository.FindByNameAsync(collectionId);
            if (tenant == null)
            {
                await Add(geometryFactory, bounds, items, typeShape, excelMetadatas, collectionId);
            }
            else
            {
                var metadataItems = excelMetadatas.Select(x =>
                    new MetadataItem { Name = x.Name, Value = x.Value, TenantId = tenant.Id }).ToList();
                if (collection.OverwriteFeatures)
                {
                    var masterCollectionStoreInfo = await UpdateMasterCollectionStoreInfo(collectionId, geometryFactory,
                        bounds, items, typeShape, excelMetadatas);
                    using (_currentTenant.Change(tenant.Id))
                    {
                        await _itemRepository.DeleteAsync(x => true, true);
                        await _metadataRepository.DeleteAsync(x => true, true);
                        await _tenantCollectionStoreRepository.DeleteAsync(x => true, true);
                        await _unitOfWork?.SaveChangesAsync()!;
                    }

                    await _unitOfWork.SaveChangesAsync();
                    Console.WriteLine($"Adding {collectionId} to tenant store");
                    metadataItems.Add(ItemsCodeStatsHelper.GetCodeStats(items));
                    await AddFeatures(tenant, masterCollectionStoreInfo, items, metadataItems.ToArray());
                }
                else if (collection.OverwriteOtherProps)
                {
                    using (_currentTenant.Change(tenant.Id))
                    {
                        await _metadataRepository.DeleteAsync(x => true, true);
                        await _unitOfWork?.SaveChangesAsync()!;
                    }

                    var updatedCol = await UpdateMasterCollectionStoreInfo(collectionId, geometryFactory,
                        bounds, items, typeShape, excelMetadatas);
                    var context = AddUserDatasetItemsJob.GetTenantContext(tenant.ConnectionStrings.First().Value);

                    Console.WriteLine($"Adding {metadataItems.Count} metadataItems to tenant");
                    await context.BulkInsertOrUpdateAsync(metadataItems);

                    updatedCol.TenantId = tenant.Id;
                    await context.BulkInsertOrUpdateAsync(new[] { updatedCol });
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
                        await _unitOfWork?.SaveChangesAsync()!;
                    }

                    var entity = await _addCollectionHelper.GetMasterCollectionStoreInfo(collectionId);
                    entity.FeatureCount += items.Count;
                    await _addCollectionHelper.SaveMasterCollectionStoreInfoAsync(entity);
                }
            }
        }

        private static void UpdateItemsWithConfScores(IList<ItemEntity> items, List<ExcelMetadata> excelMetadatas)
        {
            var lcConf = ItemsCodeStatsHelper.GetValue(excelMetadatas, ExcelMarkers.ConfidenceLandCover);
            var ctConf = ItemsCodeStatsHelper.GetValue(excelMetadatas, ExcelMarkers.ConfidenceCropType);
            //var irrConf = ItemsCodeStatsHelper.GetValue(excelMetadatas, ExcelMarkers.ConfidenceIrrigation);

            foreach (var item in items)
            {
                item.LcQualityScore = lcConf;
                item.CtQualityScore = ctConf;
                item.QualityScore = ctConf;
            }
        }

        private async Task<string> GenerateAndSaveSamples(IList<ItemEntity> items, Envelope bounds, string tempPath,
            string file)
        {
            var samples = items.Where(x => x.Extract != 0).ToArray();
            return await GeoParquetWriter.WriteDataset(samples, tempPath, file, VectorHelper.GetCoordinates(bounds),
                false);
        }

        private async Task AddDownloadUrl(List<ExcelMetadata> excelMetadatas, string filePath,
            string sampleFilePath, string harmonizationPdfFilePath, string harmonizationXlFilePath, string collectionId,
            string filesFolder)
        {
            var accessType = EntityAccessType(excelMetadatas);
            await using (var fileStream = File.OpenRead(filePath))
            {
                var url = await Upload(filePath, collectionId, accessType, fileStream);

                excelMetadatas.Add(new ExcelMetadata
                {
                    Name = ExcelMarkers.GeoParquetDownloadUrl,
                    Value = url
                });
            }

            if (File.Exists(sampleFilePath))
            {
                await using (var fileStream = File.OpenRead(sampleFilePath))
                {
                    var url = await Upload(sampleFilePath, collectionId, accessType, fileStream);
                    excelMetadatas.Add(new ExcelMetadata
                    {
                        Name = ExcelMarkers.SampleDownloadUrl,
                        Value = url
                    });
                }
            }

            if (File.Exists(harmonizationPdfFilePath))
            {
                await using (var fileStream = File.OpenRead(harmonizationPdfFilePath))
                {
                    var url = await Upload(harmonizationPdfFilePath, collectionId, accessType, fileStream);
                    excelMetadatas.Add(new ExcelMetadata
                    {
                        Name = ExcelMarkers.DatasetHarmonizationPdf,
                        Value = url
                    });
                }
            }

            if (File.Exists(harmonizationXlFilePath))
            {
                await using (var fileStream = File.OpenRead(harmonizationXlFilePath))
                {
                    var url = await Upload(harmonizationXlFilePath, collectionId, accessType, fileStream);
                    excelMetadatas.Add(new ExcelMetadata
                    {
                        Name = ExcelMarkers.DatasetHarmonizationXl,
                        Value = url
                    });
                }
            }

            var filesToIgnore = SearchHelper.GetFiles(filesFolder, "*.xlsx|*.geoparquet|*.parquet|*.pdf",
                SearchOption.AllDirectories);
            var otherFiles = Directory.GetFiles(filesFolder).Where(x => filesToIgnore.Contains(x) == false);
            foreach (var file in otherFiles)
            {
                await using (var fileStream = File.OpenRead(file))
                {
                    var url = await Upload(file, collectionId, accessType, fileStream);

                    excelMetadatas.Add(new ExcelMetadata
                    {
                        Name = ExcelMarkers.OtherFileLink,
                        Value = url
                    });
                }
            }

            Directory.Delete(filesFolder, true);
        }

        private async Task<string> Upload(string filePath, string collectionId, AccessType accessType,
            FileStream fileStream)
        {
            var fileName = new FileInfo(filePath).Name;
            var url = accessType == AccessType.Public
                ? await _azureFileUploader.UploadPublicFileAsync(collectionId, fileStream, fileName)
                : await _azureFileUploader.UploadPrivateFileAsync(collectionId, fileStream, fileName);
            return url;
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
            Console.WriteLine($"Completed adding {collectionId} to master store");

            // now update the tenant
            var tenant = await _tenantRepository.FindByNameAsync(collectionId);
            var metadataItems = excelMetadatas.Select(x =>
                new MetadataItem { Name = x.Name, Value = x.Value, TenantId = tenant.Id }).ToList();
            Console.WriteLine($"Adding {collectionId} to tenant store");
            var metadataItem = ItemsCodeStatsHelper.GetCodeStats(items);
            metadataItems.Add(metadataItem);
            await AddFeatures(tenant, insertedCollectionMetadataEntity, items, metadataItems.ToArray());
        }

        private async Task<CollectionMetadataEntity> Create(GeometryFactory geometryFactory, Envelope bounds,
            IList<ItemEntity> items, ShapeGeometryType typeShape,
            List<ExcelMetadata> excelMetadatas, string collectionId)
        {
            var masterCollectionStoreInfo =
                ProvisionUserDataSetJob.GetMasterCollectionEntity(geometryFactory, bounds, items, typeShape,
                    GetValue(excelMetadatas, ExcelMarkers.DatasetTitle),
                    collectionId);
            Console.WriteLine($"Creating database and other details-{collectionId}");
            UpdateConfScoresAndAccess(masterCollectionStoreInfo, excelMetadatas);
            var insertedCollectionMetadataEntity =
                await _addCollectionHelper.CreateMasterCollectionStoreInfo(masterCollectionStoreInfo);
            return insertedCollectionMetadataEntity;
        }

        private int GetValue(string value)
        {
            int val;
            if (int.TryParse(value, out val))
            {
                return val;
            }

            return 0;
        }

        private void UpdateConfScoresAndAccess(CollectionMetadataEntity entity, List<ExcelMetadata> excelMetadatas)
        {
            entity.AccessType =
                EntityAccessType(excelMetadatas);
            entity.StoreType = StoreType.Reference;
            entity.ConfidenceLandCover =
                GetValue(excelMetadatas.FirstOrDefault(x => x.Name == ExcelMarkers.ConfidenceCropType)?.Value ?? "0");
            entity.ConfidenceCropType =
                GetValue(excelMetadatas.FirstOrDefault(x => x.Name == ExcelMarkers.ConfidenceCropType)?.Value ?? "0");
            entity.ConfidenceIrrigationType =
                GetValue(excelMetadatas.FirstOrDefault(x => x.Name == ExcelMarkers.ConfidenceIrrigation)?.Value ?? "0");
            ;
        }

        private static AccessType EntityAccessType(List<ExcelMetadata> excelMetadatas)
        {
            return GetValue(excelMetadatas, ExcelMarkers.TypeOfLicenseAccessType).ToLowerInvariant() == "private"
                ? AccessType.Private
                : AccessType.Public;
        }

        private async Task AddFeatures(Tenant tenant, CollectionMetadataEntity insertedCollectionMetadataEntity,
            IList<ItemEntity> items, MetadataItem[] metadataItems)
        {
            var context = AddUserDatasetItemsJob.GetTenantContext(tenant.ConnectionStrings.First().Value);
            insertedCollectionMetadataEntity.TenantId = tenant.Id;
            foreach (var entity in items)
            {
                entity.TenantId = tenant.Id;
            }

            await context.BulkInsertOrUpdateAsync(new[] { insertedCollectionMetadataEntity });
            insertedCollectionMetadataEntity.TenantId = null;
            await Import(items, context);

            Console.WriteLine($"Adding {metadataItems.Length} metadataItems to tenant");
            await context.BulkInsertAsync(metadataItems);
        }

        private async Task AddFeatures(Tenant tenant, IList<ItemEntity> items)
        {
            var context = AddUserDatasetItemsJob.GetTenantContext(tenant.ConnectionStrings.First().Value);
            foreach (var entity in items)
            {
                entity.TenantId = tenant.Id;
            }

            await Import(items, context);
        }

        private async Task Import(IList<ItemEntity> items, RdmDbContext context)
        {
            var itemsCount = items.Count;
            Console.WriteLine($"Adding {itemsCount} features to tenant");
            await context.BulkInsertAsync(items, new BulkConfig { BatchSize = 10000 });
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
                Console.WriteLine($"metadata.Name-{metadata.Name},metadata.Value-{metadata.Value}");
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
            Console.WriteLine($"Downloading file from-{collection.DownloadZipUrl}");
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

            Console.WriteLine($"Unzipping file- {zipFilePath}");
            ZipFile.ExtractToDirectory(zipFilePath, filesFolder, Encoding.UTF8, true);
            File.Delete(zipFilePath);
            return filesFolder;
        }

        private async Task DownloadFromUrl(string zipFilePath, string downloadUrl)
        {
            int bufferSize = 1024;
            var timeoutMinutes = 20;
            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromMinutes(timeoutMinutes);
                using (var httpStream = await httpClient.GetStreamAsync(new Uri(downloadUrl)))
                {
                    using (var fileStream = new FileStream(zipFilePath, FileMode.Create, FileAccess.Write,
                               FileShare.None,
                               bufferSize, true))
                    {
                        await httpStream.CopyToAsync(fileStream, bufferSize, new CancellationToken());
                    }
                }
            }

            /*
            var request = new FlurlRequest
            {
                Url = new Uri(downloadUrl),
                Verb = HttpMethod.Get,
                Settings = { Timeout = TimeSpan.FromMinutes(timeoutMinutes) },
                Client = { Settings = { Timeout = TimeSpan.FromMinutes(timeoutMinutes) } }
            };
            //int bufferSize = 4096;
            var client = new FlurlClient
            {
                Settings = { Timeout = TimeSpan.FromMinutes(timeoutMinutes) },
                HttpClient = { Timeout = TimeSpan.FromMinutes(timeoutMinutes) }
            };
            using (var response = await client.SendAsync(request))
            {
                using (var httpStream = await response.GetStreamAsync())
                {
                    using (var fileStream = new FileStream(zipFilePath, FileMode.Create, FileAccess.Write,
                               FileShare.None,
                               bufferSize, true))
                    {
                        await httpStream.CopyToAsync(fileStream, bufferSize, new CancellationToken());
                    }
                }
            }*/
        }
    }
}