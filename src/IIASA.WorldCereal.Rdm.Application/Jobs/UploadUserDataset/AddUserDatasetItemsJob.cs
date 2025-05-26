using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using EFCore.BulkExtensions;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Core.Vector;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.EntityFrameworkCore;
using IIASA.WorldCereal.Rdm.Enums;
using IIASA.WorldCereal.Rdm.Jobs.Sampling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
        private readonly IBackgroundJobManager _backgroundJobManager;
        private readonly IRepository<MetadataItem, int> _metadataItemsRepository;
        private readonly ILogger<AddUserDatasetItemsJob> _logger;

        public AddUserDatasetItemsJob(IRepository<UserDataset, Guid> userDatasetRepository,
            IRepository<CollectionMetadataEntity, Guid> masterCollectionStoreRepository,
            ICurrentTenant currentTenant,
            IRepository<CollectionMetadataEntity, Guid> collectionMetadataRepository,
            ITenantRepository tenantRepository,
            IRepository<ItemEntity, long> itemRepository,
            IRepository<UserDatasetBackup, Guid> userDatasetBackups, IBackgroundJobManager backgroundJobManager,
            IRepository<MetadataItem, int> metadataItemsRepository, IAzureFileUploader backupFileUploader,
            ILogger<AddUserDatasetItemsJob> logger)
        {
            _userDatasetRepository = userDatasetRepository;
            _masterCollectionStoreRepository = masterCollectionStoreRepository;
            _currentTenant = currentTenant;
            _collectionMetadataRepository = collectionMetadataRepository;
            _tenantRepository = tenantRepository;
            _itemRepository = itemRepository;
            _userDatasetBackups = userDatasetBackups;
            _backgroundJobManager = backgroundJobManager;
            _metadataItemsRepository = metadataItemsRepository;
            _logger = logger;
        }

        public override async Task ExecuteAsync(AddUserDatasetItemsJobArgs args)
        {
            var userDataset = await _userDatasetRepository.GetAsync(x => x.Id == args.UserDatasetId);
            VectorFileReader reader = null;
            try
            {
                reader = VectorFileReader.GetReader(args.VectorFilePath);
                await AddItemsToStore(userDataset, reader);

                userDataset.State = UserDatasetState.AvailableInModule;
                userDataset.Errors = [];

                await _userDatasetBackups.InsertAsync(new UserDatasetBackup
                {
                    CollectionId = userDataset.CollectionId, UserDatasetId = userDataset.Id,
                    ZipFilePath = args.DirectoryPathToClean
                });

                _ = await _backgroundJobManager.EnqueueAsync(
                    new SamplingJobArgs { UserDatasetId = args.UserDatasetId, DirectoryPathToClean = args.DirectoryPathToClean, }, delay: TimeSpan.FromSeconds(1));
            }
            catch (Exception e)
            {
                userDataset.State = UserDatasetState.ItemsUpdateFailed;
                userDataset.Errors = new[] { e.ToString() };
            }
            finally
            {
                reader?.Dispose();
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
            var newCollectionMetadata = GetCollectionMetadata(info, tenant.Id, userDataset);

            var context = GetTenantContext(tenant.ConnectionStrings.First().Value);
            var itemsToAdd = new List<ItemEntity>();

            using (_currentTenant.Change(tenant.Id))
            {
                foreach (var item in items)
                {
                    item.TenantId = tenant.Id;
                }

                if (await _itemRepository.AnyAsync() == false)
                {
                    itemsToAdd.AddRange(items); // if new db then add all items and proceed.
                }
                else
                {
                    var sampleIdsToDelete = items.Select(x => x.SampleId);
                    var itemEntities = await _itemRepository.GetListAsync(x => sampleIdsToDelete.Contains(x.SampleId));
                    await context.BulkDeleteAsync(itemEntities);
                    itemsToAdd.AddRange(items);
                }

                var entityToUpdate = await _collectionMetadataRepository.FirstOrDefaultAsync(x =>
                    x.CollectionId == newCollectionMetadata.CollectionId);
                if (entityToUpdate == default)
                {
                    await _collectionMetadataRepository.InsertAsync(newCollectionMetadata);
                    var defaultItems = AddCollectionHelper.GetCollectionDefaultItems(newCollectionMetadata);
                    await _metadataItemsRepository.InsertManyAsync(defaultItems);
                }
                else
                {
                    Update(entityToUpdate, newCollectionMetadata);
                    await _collectionMetadataRepository.UpdateAsync(entityToUpdate);
                }

                if (itemsToAdd.Any())
                {
                    await context.BulkInsertAsync(itemsToAdd);
                }
            }
        }

        private static void Update(CollectionMetadataEntity entityToUpdate,
            CollectionMetadataEntity newCollectionMetadata)
        {
            entityToUpdate.Title = newCollectionMetadata.Title;
            entityToUpdate.Extent = newCollectionMetadata.Extent;
            entityToUpdate.EwocCodes = newCollectionMetadata.EwocCodes.Distinct().ToArray();
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
                Title = userDataset.Title,
                Type = info.Type,
                EwocCodes = info.EwocCodes.Distinct().ToArray(),
                IrrTypes = info.IrrTypes.Distinct().ToArray(),
                AccessType = info.AccessType,
                AdditionalData = info.AdditionalData,
                Extent = info.Extent,
                FeatureCount = info.FeatureCount,
                FirstDateOfValidityTime = info.FirstDateOfValidityTime,
                LastDateOfValidityTime = info.LastDateOfValidityTime
            };
        }

        public static RdmDbContext GetTenantContext(string connectionString)
        {
            DbContextOptionsBuilder<RdmDbContext> contextBuilder =
                new DbContextOptionsBuilder<RdmDbContext>();
            contextBuilder.UseNpgsql(connectionString, x => x.UseNetTopologySuite());
            return new RdmDbContext(contextBuilder.Options);
        }

       
    }
}