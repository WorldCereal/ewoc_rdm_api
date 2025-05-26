using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Core.Ai;
using IIASA.WorldCereal.Rdm.Core.Vector;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.Enums;
using IIASA.WorldCereal.Rdm.ServiceConfigs;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace IIASA.WorldCereal.Rdm.Jobs.ColumnExtraction;

public class ColumnExtractionJob : AsyncBackgroundJob<ColumnExtractionJobArgs>, ITransientDependency, IUnitOfWorkEnabled
{
    private readonly ILogger<ColumnExtractionJobArgs> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly MapPredictionService _mapPredictionService;
    private readonly IRepository<UserDataset> _userDatasetRepository;

    public ColumnExtractionJob(ILogger<ColumnExtractionJobArgs> logger, IBackgroundJobManager backgroundJobManager,
        UserDatasetConfig userDatasetConfig, MapPredictionService mapPredictionService,
        IRepository<UserDataset> userDatasetRepository)
    {
        _logger = logger;
        _backgroundJobManager = backgroundJobManager;
        _mapPredictionService = mapPredictionService;
        _userDatasetRepository = userDatasetRepository;
    }

    public override async Task ExecuteAsync(ColumnExtractionJobArgs args)
    {
        var userDataset = await _userDatasetRepository.GetAsync(x => x.Id == args.UserDatasetId);
        VectorFileReader? reader = null;

        _logger.LogInformation($"Harmonizing user dataset-{userDataset.CollectionId}.File- {args.Path}");

        try
        {
            var res = await _mapPredictionService.CheckAndReProjectTo4326(args.Path);
            _logger.LogInformation(res);
            var parquetPath = SearchHelper.GetFilePath(args.DatasetExtractedPath);
            reader = VectorFileReader.GetReader(parquetPath);
            var columns = reader.GetAllFieldName();
            var colNames = new List<string>();
            foreach (var column in columns)
            {
                var ordinal = reader.GetOrdinalExt(column);
                var type = reader.GetFieldType(ordinal);
                colNames.Add($"{column};{type}");
            }

            userDataset.Columns = colNames.ToArray();
            userDataset.State = UserDatasetState.UploadedColumnExtracted;
            await _userDatasetRepository.UpdateAsync(userDataset, true);
        }
        catch (Exception e)
        {
            userDataset.State = UserDatasetState.UploadedColumnExtractFailed;
            userDataset.Errors = ["Internal Server Error in UserDatasetHarmoniseJob."];
            _logger.LogError($"Error Harmonising - {e}");
        }
        finally
        {
            reader?.Dispose();
        }
    }
}