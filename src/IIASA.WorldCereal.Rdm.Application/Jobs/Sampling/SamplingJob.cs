using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Core.Ai;
using IIASA.WorldCereal.Rdm.Core.Vector;
using IIASA.WorldCereal.Rdm.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace IIASA.WorldCereal.Rdm.Jobs.Sampling;

public class SamplingJob : AsyncBackgroundJob<SamplingJobArgs>, ITransientDependency, IUnitOfWorkEnabled
{
    private readonly IRepository<UserDataset> _userDatasetRepository;
    private readonly MapPredictionService _mapPredictionService;
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<ItemEntity, long> _itemRepository;
    private readonly IRepository<CollectionMetadataEntity, Guid> _collectionMetadataRepository;
    private readonly IAzureFileUploader _azureFileUploader;
    private readonly ILogger<SamplingJob> _logger;

    public SamplingJob(IRepository<UserDataset> userDatasetRepository, MapPredictionService mapPredictionService,
        ICurrentTenant currentTenant, ITenantRepository tenantRepository, IRepository<ItemEntity, long> itemRepository,
        IRepository<CollectionMetadataEntity, Guid> collectionMetadataRepository,
        IAzureFileUploader azureFileUploader, ILogger<SamplingJob> logger)
    {
        _userDatasetRepository = userDatasetRepository;
        _mapPredictionService = mapPredictionService;
        _currentTenant = currentTenant;
        _tenantRepository = tenantRepository;
        _itemRepository = itemRepository;
        _collectionMetadataRepository = collectionMetadataRepository;
        _azureFileUploader = azureFileUploader;
        _logger = logger;
    }

    public override async Task ExecuteAsync(SamplingJobArgs args)
    {
        var userDataset = await _userDatasetRepository.GetAsync(x => x.Id == args.UserDatasetId);
        var collectionId = userDataset.CollectionId;
        var dbName = AddCollectionHelper.GetDbName(collectionId);
        try
        {
            if (await _mapPredictionService.DoH3Indexing(dbName))
            {
                await _mapPredictionService.DoSampleExtraction(dbName);
            }  
        }
        finally
        {
            await BackupFilesAndClean(args, userDataset);
        }
    }

    private async Task BackupFilesAndClean(SamplingJobArgs args, UserDataset userDataset)
    {
        try
        {
            var tenant = await _tenantRepository.FindByNameAsync(userDataset.CollectionId);
            ItemEntity[] items = [];
            Coordinate[] bbox = [];
            using (_currentTenant.Change(tenant.Id))
            {
                items = await (await _itemRepository.GetQueryableAsync()).AsNoTracking().ToArrayAsync();
                bbox = (await _collectionMetadataRepository.FirstAsync()).Extent.Coordinates;
            }
            
            await GeoParquetWriter.WriteDataset(items, args.DirectoryPathToClean, userDataset.CollectionId, bbox,
                false);
            await GeoParquetWriter.WriteDataset(items.Where(x => x.Extract != 0).ToArray(), args.DirectoryPathToClean,
                $"sample_{userDataset.CollectionId}", bbox, false);

            var files = Directory.GetFiles(args.DirectoryPathToClean);
            foreach (var file in files)
            {
                await using (var streamFile = File.OpenRead(file))
                {
                    await _azureFileUploader.UploadUserDatasetFileAsync(userDataset.Id.ToString("N"), streamFile,
                        Path.GetFileName(file));
                }
            }

            CleanFiles(args.DirectoryPathToClean);
        }
        catch (Exception e)
        {
            _logger.LogError(e.ToString());
        }
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