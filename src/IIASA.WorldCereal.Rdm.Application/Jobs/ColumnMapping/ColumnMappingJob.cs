using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Core.Ai;
using IIASA.WorldCereal.Rdm.Core.Vector;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.Enums;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace IIASA.WorldCereal.Rdm.Jobs.ColumnMapping;

public class ColumnMappingJob : AsyncBackgroundJob<ColumnMappingJobArgs>, ITransientDependency, IUnitOfWorkEnabled
{
    private readonly ILogger<ColumnMappingJobArgs> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly MapPredictionService _mapPredictionService;
    private readonly IRepository<UserDataset> _userDatasetRepository;

    public ColumnMappingJob(ILogger<ColumnMappingJobArgs> logger, IBackgroundJobManager backgroundJobManager,
        MapPredictionService mapPredictionService,
        IRepository<UserDataset> userDatasetRepository)
    {
        _logger = logger;
        _backgroundJobManager = backgroundJobManager;
        _mapPredictionService = mapPredictionService;
        _userDatasetRepository = userDatasetRepository;
    }

    public override async Task ExecuteAsync(ColumnMappingJobArgs args)
    {
        var userDataset = await _userDatasetRepository.GetAsync(x => x.Id == args.UserDatasetId);
        VectorFileReader? reader = null;
        userDataset.Errors = [];

        _logger.LogInformation($"Harmonizing user dataset-{userDataset.CollectionId}.File- {args.Path}");
        char[] separators = ['T', ' '];
        try
        {
            reader = VectorFileReader.GetReader(args.Path);
            var ewocIndex = reader.GetOrdinalExt(args.EwocColumnName);
            var ewocColType = reader.GetFieldType(ewocIndex);
            var irrIndex = reader.GetOrdinalExt(args.IrrColumnName);
            var irrColType = irrIndex != -1 ? reader.GetFieldType(irrIndex) : typeof(int);
            var rawEwocList = new List<string>();
            var rawIrrList = new List<string>();

            var indexOfValTimeCol = -1;
            if (args.IsManualValidityDate == false)
            {
                indexOfValTimeCol = reader.GetOrdinalExt(userDataset.ValDateCol);
            }

            var listOfYears = new Dictionary<int, int>();
            while (reader.Read())
            {
                var rawEwoc = reader.GetValueWithType(ewocIndex, ewocColType);
                rawEwocList.Add(rawEwoc);

                if (irrIndex != -1)
                {
                    var rawIrr = reader.GetValueWithType(irrIndex, irrColType);
                    rawIrrList.Add(rawIrr);
                }


                if (args.IsManualValidityDate)
                {
                    continue;
                }

                // sample year calculations
                DateTime sampleDate;
                if (args.IsValAttributeDateTimeType)
                {
                    sampleDate = reader.GetDateTimeExt(indexOfValTimeCol);
                }
                else
                {
                    var strDate = reader.GetStringExt(indexOfValTimeCol);
                    sampleDate = DateTime.ParseExact(strDate.Split(separators)[0], args.DateFormat,
                        CultureInfo.InvariantCulture);
                }

                var year = sampleDate.Year;
                if (listOfYears.TryGetValue(year, out _))
                {
                    listOfYears[year] += 1;
                }
                else
                {
                    listOfYears.Add(year, 1);
                }
            }

            if (args.IsManualValidityDate)
            {
                listOfYears.Add(
                    DateTime.ParseExact(userDataset.ValDateCol, "yyyy-M-d", CultureInfo.InvariantCulture).Year, 1);
            }

            rawEwocList = rawEwocList.Distinct().Order().ToList();
            await WriteCropMaps(args, rawEwocList, userDataset);
            rawIrrList = rawIrrList.Distinct().Order().ToList();
            if (irrIndex != -1 && rawIrrList.Any())
            {
                await WriteIrrMaps(args, rawIrrList, userDataset);
            }

            await DateParseHelper.SaveAsync(args.DateFormat, args.IsManualValidityDate, args.IsValAttributeDateTimeType,
                userDataset.DatasetExtractedPath,
                listOfYears.OrderBy(x => x.Key).Select(x => $"{x.Key};{x.Value}").ToArray());
            userDataset.State = UserDatasetState.ColumnMappingExtracted;

            await _userDatasetRepository.UpdateAsync(userDataset, true);
        }
        catch (Exception e)
        {
            userDataset.State = UserDatasetState.ColumnMappingExtractFailed;
            userDataset.Errors = [..userDataset.Errors, "Error While running ColumnMappingJob.", e.ToString()];
            _logger.LogError($"Error Harmonising - {e}");
        }
        finally
        {
            reader?.Dispose();
        }
    }

    private async Task WriteIrrMaps(ColumnMappingJobArgs args, List<string> rawIrrList, UserDataset userDataset)
    {
        var predictions = await GetIrrPredictions(rawIrrList, userDataset);
        if (predictions == null)
        {
            await WriteToCsv(rawIrrList, ColMapConstants.IrrFileName, args.DatasetExtractedPath);
        }
        else
        {
            await WriteToCsv(predictions
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value == -1 ? "0" : kvp.Value.ToString()
                ), ColMapConstants.IrrFileName, args.DatasetExtractedPath);
        }
    }

    private async Task WriteCropMaps(ColumnMappingJobArgs args, List<string> rawEwocList, UserDataset userDataset)
    {
        var predictions = await GetCropPredictions(rawEwocList, userDataset);
        if (predictions == null)
        {
            await WriteToCsv(rawEwocList, ColMapConstants.EwocFileName, args.DatasetExtractedPath);
        }
        else
        {
            await WriteToCsv(predictions
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value == -1 ? "0" : kvp.Value.ToString()
                ), ColMapConstants.EwocFileName, args.DatasetExtractedPath);
        }
    }

    private async Task<Dictionary<string, int>?> GetIrrPredictions(List<string> rawEwocList, UserDataset userDataset)
    {
        var retry = 3;
        var errorMsg = string.Empty;
        while (retry > 0)
        {
            try
            {
                return await _mapPredictionService.GetIrrigationPredictions(rawEwocList.ToArray());
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            Thread.Sleep(TimeSpan.FromSeconds(1));
            retry--;
        }

        userDataset.Errors = [..userDataset.Errors, errorMsg];
        return null;
    }

    private async Task<Dictionary<string, long>?> GetCropPredictions(List<string> rawEwocList, UserDataset userDataset)
    {
        var retry = 3;
        var errorMsg = "";
        while (retry > 0)
        {
            try
            {
                return await _mapPredictionService.GetCropPredictions(rawEwocList.ToArray());
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                errorMsg = e.ToString();
            }

            Thread.Sleep(TimeSpan.FromSeconds(1));
            retry--;
        }

        userDataset.Errors = [..userDataset.Errors, errorMsg];
        return null;
    }

    private static async Task WriteToCsv(List<string> rawEwocList, string fileName, string dirPath)
    {
        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine($"Label,Code");
        var distintValues = rawEwocList.Distinct().Order();
        foreach (var item in distintValues)
        {
            csvBuilder.AppendLine($"\"{item.Replace(",", ";")}\",0");
        }

        await File.WriteAllTextAsync(Path.Combine(dirPath, fileName + ".csv"), csvBuilder.ToString());
    }

    private static async Task WriteToCsv(Dictionary<string, string> predictions, string fileName, string dirPath)
    {
        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine($"Label,Code");
        foreach (var (key, value) in predictions)
        {
            csvBuilder.AppendLine($"\"{key.Replace(",", ";")}\",{value}");
        }

        await File.WriteAllTextAsync(Path.Combine(dirPath, fileName + ".csv"), csvBuilder.ToString());
    }
}

public static class ColMapConstants
{
    public const string EwocFileName = "ewocMap";
    public const string EwocFileNameWithExt = $"{EwocFileName}.csv";
    public const string EwocMappedFileNameWithExt = $"{EwocFileName}ped.csv";
    public const string IrrFileName = "irrMap";
    public const string IrrFileNameWithExt = $"{IrrFileName}.csv";
    public const string IrrMappedFileNameWithExt = $"{IrrFileName}ped.csv";
}