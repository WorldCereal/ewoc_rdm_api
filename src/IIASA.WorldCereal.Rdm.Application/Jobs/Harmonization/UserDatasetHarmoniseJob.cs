using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Core.Ai;
using IIASA.WorldCereal.Rdm.Core.Vector;
using IIASA.WorldCereal.Rdm.Dtos;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.Enums;
using IIASA.WorldCereal.Rdm.Jobs.Validation;
using Microsoft.Extensions.Logging;
using NetTopologySuite.IO;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace IIASA.WorldCereal.Rdm.Jobs.Harmonization;

public class UserDatasetHarmoniseJob : AsyncBackgroundJob<HarmoniseArgs>, ITransientDependency, IUnitOfWorkEnabled
{
    private readonly ILogger<UserDatasetHarmoniseJob> _logger;
    private readonly ITenantRepository _tenantRepository;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly MapPredictionService _mapPredictionService;
    private readonly IRepository<UserDataset> _userDatasetRepository;

    public UserDatasetHarmoniseJob(ILogger<UserDatasetHarmoniseJob> logger, ITenantRepository tenantRepository,
        IBackgroundJobManager backgroundJobManager, MapPredictionService mapPredictionService,
        IRepository<UserDataset> userDatasetRepository)
    {
        _logger = logger;
        _tenantRepository = tenantRepository;
        _backgroundJobManager = backgroundJobManager;
        _mapPredictionService = mapPredictionService;
        _userDatasetRepository = userDatasetRepository;
    }

    public override async Task ExecuteAsync(HarmoniseArgs args)
    {
        var userDataset = await _userDatasetRepository.GetAsync(x => x.Id == args.UserDatasetId);
        VectorFileReader? reader = null;

        _logger.LogInformation($"Harmonizing user dataset-{userDataset.CollectionId}.File- {args.Path}");
        try
        {
            reader = VectorFileReader.GetReader(args.Path);
            await Harmonise(userDataset, reader, args);
            userDataset.State = UserDatasetState.UploadedValidationInProgressWait;
            userDataset.Errors = [];
        }
        catch (Exception e)
        {
            userDataset.State = UserDatasetState.ValidationFailedUploadRequired;
            userDataset.Errors = ["Internal Server Error in UserDatasetHarmoniseJob."];
            _logger.LogError($"Error Harmonising - {e}");
        }
        finally
        {
            reader?.Dispose();
            await _userDatasetRepository.UpdateAsync(userDataset);
        }
    }

    private async Task Harmonise(UserDataset userDataset, VectorFileReader reader, HarmoniseArgs args)
    {
        var indexOfEwocCol = reader.GetOrdinalExt(userDataset.EwocCodeCol);

        var dateVal = DateTime.Now;
        var dateInfo = await DateParseHelper.GetDateInfo(userDataset.DatasetExtractedPath);
        var indexOfValTimeCol = -1;
        if (dateInfo.IsManualDate == false)
        {
            indexOfValTimeCol = reader.GetOrdinalExt(userDataset.ValDateCol);
        }
        else
        {
            dateVal = DateTime.ParseExact(userDataset.ValDateCol, "yyyy-M-d",
                CultureInfo.InvariantCulture);
        }

        var indexOfIrr = reader.GetOrdinalExt(userDataset.IrrCol);
        var ewcoMaps = CodeMapHelper.GetMap(args.EwocMappingFile);
        var irrMaps = Array.Empty<CodeMap>();
        if (indexOfIrr != -1 && string.IsNullOrWhiteSpace(args.IrrMappingFile) == false)
        {
            irrMaps = CodeMapHelper.GetMap(args.IrrMappingFile);
        }

        var bbox = VectorHelper.GetCoordinates(reader.GetBounds());
        var items = new List<ItemEntity>();
        var index = 1;
        while (reader.Read())
        {
            try
            {
                ItemEntity entity = GetEntity(reader, userDataset.CollectionId, ewcoMaps, irrMaps, indexOfEwocCol,
                    indexOfValTimeCol, indexOfIrr, index, dateVal, dateInfo);
                if (entity.ValidityTime.Year != dateInfo.SelectedYear)
                {
                    continue;
                }

                entity.Geometry.SRID = GeoJsonHelper.GeometryWgs84Srid;
                items.Add(entity);
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Harmonising Item - {e}");
            }

            index++;
        }

        var landInfo = CollectionIdGenerator.GetInfoConent(items);
        var wktBboxGeom = CollectionIdGenerator.GetWktPolygon(items);
        var region = await _mapPredictionService.GetRegion(wktBboxGeom);
        var year = ((await DateParseHelper.GetDateInfo(userDataset.DatasetExtractedPath))!).SelectedYear
            .ToString();
        var typeShape = reader.GetShapeGeometryType();
        var newColId = CollectionIdGenerator.GetColId(year, region, userDataset.CollectionId, typeShape, landInfo);
        userDataset.CollectionId = await GetUniqueCollectionId(newColId);
        userDataset.Title = newColId.ToUpperInvariant();

        UpdateCollectionId(items, userDataset.CollectionId);

        var filePath =
            await GeoParquetWriter.WriteDataset(items, args.DatasetExtractedPath, userDataset.CollectionId, bbox, true);
        _logger.LogInformation($"File path-{filePath}, starting validation job");
        var validationArgs = new ValidationArgs
        {
            VectorFilePath = filePath, UserDatasetId = userDataset.Id,
            DatasetExtractedPath = args.DatasetExtractedPath
        };
        await _backgroundJobManager.EnqueueAsync(validationArgs, delay: TimeSpan.FromSeconds(1));
    }

    private static void UpdateCollectionId(List<ItemEntity> items, string userDatasetCollectionId)
    {
        var index = 1;
        foreach (var entity in items)
        {
            entity.CollectionId = userDatasetCollectionId;
            entity.SampleId = $"{userDatasetCollectionId}_{index++}";
        }
    }

    private async Task<string> GetUniqueCollectionId(string collectionId)
    {
        var index = 1;
        var generatedCol = collectionId;
        while (await _tenantRepository.FindByNameAsync(generatedCol) != null)
        {
            var fragments = collectionId.Split("_");
            fragments[2] += index++;
            generatedCol = string.Join("_", fragments);
        }

        return generatedCol;
    }

    private ItemEntity GetEntity(VectorFileReader vectorFileReader, string userDatasetCollectionId,
        IEnumerable<CodeMap> colMap, IEnumerable<CodeMap> irrMap,
        int indexOfEwocCol, int indexOfValTimeCol, int indexOfIrr, int index, DateTime dateVal, DateInfo dateInfo)
    {
        var ewocType = vectorFileReader.GetFieldType(indexOfEwocCol);
        var ewcoCode = vectorFileReader.GetValueWithType(indexOfEwocCol, ewocType);

        var irrColType = indexOfIrr != -1 ? vectorFileReader.GetFieldType(indexOfIrr) : typeof(int);
        var irrCode = vectorFileReader.GetValueWithType(indexOfIrr, irrColType);

        try
        {
            var valTime = GetValDateTime(vectorFileReader, indexOfValTimeCol, dateVal, dateInfo);
            var codeMap = colMap.FirstOrDefault(x =>
                string.Compare(x.Label, ewcoCode, StringComparison.Ordinal) == 0);
            var irrCodeMap =
                irrMap.FirstOrDefault(x =>
                    string.Compare(x.Label, irrCode, StringComparison.Ordinal) == 0);
            return new ItemEntity
            {
                CollectionId = userDatasetCollectionId,
                EwocCode = codeMap?.Code ?? 0,
                IrrigationStatus = indexOfIrr == -1
                    ? 0
                    : (int)(irrCodeMap?.Code ?? 0),
                SampleId = string.Concat(userDatasetCollectionId, "-", index),
                ValidityTime = valTime,
                Geometry = vectorFileReader.Geometry,

                //optional
                ImageryDate = null,
                NumberOfValidations = null,
                TypeOfValidator = null,
                AgreementOfObservations = null,
                DisAgreementOfObservations = null,
                H3Index = string.Empty,
                Extract = 0,
            };
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            _logger.LogError($"Error Harmonising Item - {e}");
            throw;
        }
    }

    private static DateTime GetValDateTime(VectorFileReader vectorFileReader, int indexOfValTimeCol, DateTime dateVal,
        DateInfo dateInfo)
    {
        if (indexOfValTimeCol == -1)
        {
            return dateVal;
        }

        if (dateInfo.IsValAttributeDateTimeType)
        {
            return vectorFileReader.GetDateTimeExt(indexOfValTimeCol);
        }

        char[] separators = ['T', ' '];
        var strDate = vectorFileReader.GetStringExt(indexOfValTimeCol);
        return DateTime.ParseExact(strDate.Split(separators)[0], dateInfo.DateFormat, CultureInfo.InvariantCulture);
    }


    private static void ThrowError(UserDataset userDataset, string msg)
    {
        userDataset.Errors = userDataset.Errors.Concat(new[] { msg }).ToArray();
        throw new AbpException(msg);
    }
}