using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper.Internal;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.EntityFrameworkCore;
using IIASA.WorldCereal.Rdm.Enums;
using IIASA.WorldCereal.Rdm.ServiceConfigs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql.Bulk;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace IIASA.WorldCereal.Rdm.Jobs.UploadUserDataset
{
    public class AddUserDatasetItemsJob : AsyncBackgroundJob<AddUserDatasetItemsJobArgs>, ITransientDependency,
        IUnitOfWorkEnabled
    {
        private readonly IRepository<UserDataset, Guid> _userDatasetRepository;
        private readonly IRepository<CollectionMetadataEntity, Guid> _masterCollectionStoreRepository;
        private readonly ICurrentTenant _currentTenant;
        private readonly IRepository<CollectionMetadataEntity, Guid> _collectionMetadataRepository;
        private readonly ITenantRepository _tenantRepository;
        private readonly IRepository<ItemEntity, long> _itemRepository;
        private readonly IRepository<UserDatasetBackup, Guid> _userDatasetBackups;
        private readonly UserDatasetConfig _userDatasetConfig;
        private readonly ILogger<AddUserDatasetItemsJob> _logger;

        public AddUserDatasetItemsJob(IRepository<UserDataset, Guid> userDatasetRepository,
            IRepository<CollectionMetadataEntity, Guid> masterCollectionStoreRepository,
            ICurrentTenant currentTenant,
            IRepository<CollectionMetadataEntity, Guid> collectionMetadataRepository,
            ITenantRepository tenantRepository,
            IRepository<ItemEntity, long> itemRepository,
            IRepository<UserDatasetBackup, Guid> userDatasetBackups,
            UserDatasetConfig userDatasetConfig,
            ILogger<AddUserDatasetItemsJob> logger)
        {
            _userDatasetRepository = userDatasetRepository;
            _masterCollectionStoreRepository = masterCollectionStoreRepository;
            _currentTenant = currentTenant;
            _collectionMetadataRepository = collectionMetadataRepository;
            _tenantRepository = tenantRepository;
            _itemRepository = itemRepository;
            _userDatasetBackups = userDatasetBackups;
            _userDatasetConfig = userDatasetConfig;
            _logger = logger;
        }

        public override async Task ExecuteAsync(AddUserDatasetItemsJobArgs args)
        {
            var userDataset = await _userDatasetRepository.GetAsync(x => x.Id == args.UserDatasetId);
            VectorFileReader reader = null;
            try
            {
                reader = VectorFileReader.GetReader(args.Path);
                await AddItemsToStore(userDataset, reader);

                userDataset.State = UserDatasetState.AvailableInModule;
                userDataset.Errors = Array.Empty<string>();

                await _userDatasetBackups.InsertAsync(new UserDatasetBackup
                {
                    CollectionId = userDataset.CollectionId, UserDatasetId = userDataset.Id,
                    ZipFilePath = args.ZipFilePath.Replace(_userDatasetConfig.DatasetBackupFolder, string.Empty)
                });
            }
            catch (Exception e)
            {
                userDataset.State = UserDatasetState.ItemsUpdateFailed;
                userDataset.Errors = new[] {e.ToString()};
            }
            finally
            {
                reader?.Dispose();
                CleanFiles(args.DirectoryPathToClean);
            }

            await _userDatasetRepository.UpdateAsync(userDataset);
        }

        private async Task AddItemsToStore(UserDataset userDataset, VectorFileReader reader)
        {
            var typeShape = reader.GetShapeGeometryType();
            var allFieldNames = reader.GetAllFieldName();
            var bounds = reader.GetBounds();

            var shapeFileUpdater = new ShapeFileUpdater();

            var items = shapeFileUpdater.ExtractItemsToSave(reader, userDataset.CollectionId);

            var info = await _masterCollectionStoreRepository.GetAsync(x =>
                x.CollectionId == userDataset.CollectionId);

            var tenant = await _tenantRepository.FindByNameAsync(userDataset.CollectionId);
            CollectionMetadataEntity newCollectionMetadata = GetCollectionMetadata(info, tenant.Id, userDataset);

            NpgsqlBulkUploader uploader = await GetDbUploader(userDataset.CollectionId, _tenantRepository);

            var batchCount = 1000;
            var count = items.Count;

            var itemsToAdd = new List<ItemEntity>();
            var itemsToUpdate = new List<ItemEntity>();


            using (_currentTenant.Change(tenant.Id))
            {
                if (await _itemRepository.AnyAsync() == false)
                {
                    items.ForAll(x=>x.TenantId = tenant.Id);
                    itemsToAdd.AddRange(items); // if new db then add all items and proceed.
                }
                else
                {
                    foreach (var item in items)
                    {
                        item.TenantId = tenant.Id;
                        if (await _itemRepository.AnyAsync(x => x.SampleId == item.SampleId))
                        {
                            var itemToUpdate = await _itemRepository.FindAsync(x => x.SampleId == item.SampleId);
                            Update(itemToUpdate, item);
                            itemsToUpdate.Add(item);
                        }
                        else
                        {
                            itemsToAdd.Add(item);
                        }
                    }
                }

                var entityToUpdate = await _collectionMetadataRepository.FirstOrDefaultAsync(x =>
                    x.CollectionId == newCollectionMetadata.CollectionId);
                if (entityToUpdate == default)
                {
                    await _collectionMetadataRepository.InsertAsync(newCollectionMetadata);
                }
                else
                {
                    Update(entityToUpdate, newCollectionMetadata);
                    await _collectionMetadataRepository.UpdateAsync(entityToUpdate);
                }
            }

            if (itemsToAdd.Any())
            {
                for (int i = 0; i < count; i += batchCount)
                {
                    var batch = itemsToAdd.Skip(i).Take(batchCount);
                    await uploader.ImportAsync(batch);
                }
            }

            if (itemsToUpdate.Any())
            {
                for (int i = 0; i < count; i += batchCount)
                {
                    var batch = itemsToUpdate.Skip(i).Take(batchCount);
                    await uploader.UpdateAsync(batch);
                }
            }
        }

        private static void Update(ItemEntity itemToUpdate, ItemEntity item)
        {
            itemToUpdate.Area = item.Area;
            itemToUpdate.Ct = item.Ct;
            itemToUpdate.Lc = item.Lc;
            itemToUpdate.Irr = item.Irr;
            itemToUpdate.Geometry = item.Geometry;
            itemToUpdate.Split = item.Split;
            itemToUpdate.ImageryTime = item.ImageryTime;
            itemToUpdate.ValidityTime = item.ValidityTime;
            itemToUpdate.NumberOfValidations = item.NumberOfValidations;
            itemToUpdate.UserConf = item.UserConf;
            itemToUpdate.AgreementOfObservations = item.AgreementOfObservations;
            itemToUpdate.DisAgreementOfObservations = item.DisAgreementOfObservations;
            itemToUpdate.TypeOfValidator = item.TypeOfValidator;
        }

        private static void Update(CollectionMetadataEntity entityToUpdate,
            CollectionMetadataEntity newCollectionMetadata)
        {
            entityToUpdate.Title = newCollectionMetadata.Title;
            entityToUpdate.Extent = newCollectionMetadata.Extent;
            entityToUpdate.ConfidenceCropType = newCollectionMetadata.ConfidenceCropType;
            entityToUpdate.ConfidenceLandCover = newCollectionMetadata.ConfidenceLandCover;
            entityToUpdate.ConfidenceIrrigationType = newCollectionMetadata.ConfidenceIrrigationType;
            entityToUpdate.LandCovers = newCollectionMetadata.LandCovers.Distinct().ToArray();
            entityToUpdate.CropTypes = newCollectionMetadata.CropTypes.Distinct().ToArray();
            entityToUpdate.IrrTypes = newCollectionMetadata.IrrTypes.Distinct().ToArray();
            entityToUpdate.Extent = newCollectionMetadata.Extent;
            entityToUpdate.FeatureCount = newCollectionMetadata.FeatureCount;
            entityToUpdate.FirstDateOfValidityTime = newCollectionMetadata.FirstDateOfValidityTime;
            entityToUpdate.LastDateOfValidityTime = newCollectionMetadata.LastDateOfValidityTime;
        }

        private static CollectionMetadataEntity GetCollectionMetadata(CollectionMetadataEntity info, Guid tenantId,
            UserDataset userDataset)
        {
            return new()
            {
                TenantId = tenantId,
                CollectionId = userDataset.CollectionId,
                Description = string.Empty,
                Title = userDataset.Title,
                Type = info.Type,
                TypeOfObservationMethod = userDataset.TypeOfObservationMethod,
                ConfidenceLandCover = userDataset.ConfidenceLandCover,
                ConfidenceCropType = userDataset.ConfidenceCropType,
                ConfidenceIrrigationType = userDataset.ConfidenceIrrigationType,
                LandCovers = info.LandCovers.Distinct().ToArray(),
                CropTypes = info.CropTypes.Distinct().ToArray(),
                IrrTypes = info.IrrTypes.Distinct().ToArray(),
                AccessType = info.AccessType,
                AdditionalData = info.AdditionalData,
                Extent = info.Extent,
                FeatureCount = info.FeatureCount,
                FirstDateOfValidityTime = info.FirstDateOfValidityTime,
                LastDateOfValidityTime = info.LastDateOfValidityTime
            };
        }

        private static async Task<NpgsqlBulkUploader> GetDbUploader(string collectionId, ITenantRepository tenantRepository)
        {
            var tenant = await tenantRepository.FindByNameAsync(collectionId);
            var context = GetTenantContext(tenant.ConnectionStrings.First().Value);
            NpgsqlBulkUploader uploader = new NpgsqlBulkUploader(context);
            return uploader;
        }

        public static RdmDbContext GetTenantContext(string connectionString)
        {
            DbContextOptionsBuilder<RdmDbContext> contextBuilder =
                new DbContextOptionsBuilder<RdmDbContext>();
            contextBuilder.UseNpgsql(connectionString, x => x.UseNetTopologySuite());
            return new RdmDbContext(contextBuilder.Options);
        }

        private void CleanFiles(string directoryPathToClean)
        {
            try
            {
                Directory.Delete(directoryPathToClean, true);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while deleting files");
            }
        }
    }
}