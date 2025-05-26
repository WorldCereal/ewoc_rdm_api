using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Flurl.Util;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Core.Ai;
using IIASA.WorldCereal.Rdm.Core.Vector;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.Enums;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace IIASA.WorldCereal.Rdm.Jobs.UploadUserDataset
{
    public class ProvisionUserDataSetJob : AsyncBackgroundJob<ProvisionUserDataSetJobArgs>, ITransientDependency,
        IUnitOfWorkEnabled
    {
        private readonly IRepository<UserDataset, Guid> _userDatasetRepository;
        private readonly IBackgroundJobManager _backgroundJobManager;
        private readonly MapPredictionService _mapPredictionService;
        private readonly IAddCollectionHelper _addCollectionHelper;
        private readonly ILogger<ProvisionUserDataSetJob> _logger;

        public ProvisionUserDataSetJob(IRepository<UserDataset, Guid> userDatasetRepository,
            IBackgroundJobManager backgroundJobManager, MapPredictionService mapPredictionService,
            IAddCollectionHelper addCollectionHelper, ILogger<ProvisionUserDataSetJob> logger)
        {
            _userDatasetRepository = userDatasetRepository;
            _backgroundJobManager = backgroundJobManager;
            _mapPredictionService = mapPredictionService;
            _addCollectionHelper = addCollectionHelper;
            _logger = logger;
        }

        public override async Task ExecuteAsync(ProvisionUserDataSetJobArgs args)
        {
            // run this job only after validation is successful for the dataset.

            var userDataset = await _userDatasetRepository.GetAsync(x => x.Id == args.UserDatasetId);
            _logger.LogInformation($"Started provisioning the dataset- {userDataset.Title}");

            try
            {
                var geometryFactory = new GeometryFactory(new PrecisionModel(), GeoJsonHelper.GeometryWgs84Srid);

                var typeShape = GetShapeGeometryType(geometryFactory, out var bounds,
                    out var items, userDataset.CollectionId, args.VectorFilePath);

                var masterCollectionStore =
                    await _addCollectionHelper.GetMasterCollectionStoreInfo(userDataset.CollectionId);

                if (masterCollectionStore == default)
                {
                    CollectionMetadataEntity masterCollectionStoreInfo =
                        GetMasterCollectionEntity(geometryFactory, bounds, items, typeShape, userDataset.Title,
                            userDataset.CollectionId);


                    masterCollectionStore =
                        await _addCollectionHelper.CreateMasterCollectionStoreInfo(masterCollectionStoreInfo);
                }
                else
                {
                    Update(masterCollectionStore, userDataset, geometryFactory, bounds, items, typeShape);
                    await _addCollectionHelper.SaveMasterCollectionStoreInfoAsync(masterCollectionStore);
                }

                userDataset.MasterCollectionStoreInfoId = masterCollectionStore.Id;
                userDataset.State = UserDatasetState.StoreProvisioned;
                userDataset.Errors = Array.Empty<string>();

                // cannot save the items here as the tenant would be updated in db only after the unit of work is completed. So need to update in next job.
                await StartNextJob(args);
            }
            catch (Exception e)
            {
                userDataset.State = UserDatasetState.StoreProvisionFailed;
                userDataset.Errors = [e.ToString()];
            }
            finally
            {
                await _userDatasetRepository.UpdateAsync(userDataset);
            }
        }

        public static ShapeGeometryType GetShapeGeometryType(GeometryFactory geometryFactory, out Envelope bounds,
            out IList<ItemEntity> items, string collectionId, string filePath)
        {
            VectorFileReader reader = null;
            try
            {
                reader = VectorFileReader.GetReader(filePath);

                var typeShape = reader.GetShapeGeometryType();
                var allFieldNames = reader.GetAllFieldName();
                bounds = reader.GetBounds();

                var shapeFileUpdater = new ShapeFileUpdater();

                items = shapeFileUpdater.ExtractItemsToSave(reader, collectionId);
                reader.Dispose();
                return typeShape;
            }
            finally
            {
                reader?.Dispose();
            }
        }

        private async Task StartNextJob(ProvisionUserDataSetJobArgs args)
        {
            var itemsUpdate = new AddUserDatasetItemsJobArgs
            {
                UserDatasetId = args.UserDatasetId, VectorFilePath = args.VectorFilePath,
                DirectoryPathToClean = args.DatasetExtractedPath
            };
            _ = await _backgroundJobManager.EnqueueAsync(itemsUpdate, delay: TimeSpan.FromSeconds(1));
        }

        private static void Update(CollectionMetadataEntity entity, UserDataset userDataset,
            GeometryFactory geometryFactory, Envelope bounds, IList<ItemEntity> items, ShapeGeometryType typeShape)
        {
            var dateOrderedItems = items.Select(x => x.ValidityTime).ToList();
            dateOrderedItems.Add(entity.FirstDateOfValidityTime);
            dateOrderedItems.Add(entity.LastDateOfValidityTime);
            dateOrderedItems = dateOrderedItems.OrderBy(x => x).ToList();

            entity.Title = userDataset.Title;
            entity.CollectionId = userDataset.CollectionId;
            entity.Type = GetCollectionType(typeShape);
            entity.Extent.EnvelopeInternal.ExpandToInclude(
                new Polygon(new LinearRing(VectorHelper.GetCoordinates(bounds)), geometryFactory).EnvelopeInternal);
            entity.FeatureCount = entity.FeatureCount + items.Count;
            entity.EwocCodes = entity.EwocCodes.Concat(items.Select(c => c.EwocCode)).Distinct().ToArray();
            entity.IrrTypes = entity.IrrTypes.Concat(items.Select(c => c.IrrigationStatus)).Distinct().ToArray();
            entity.FirstDateOfValidityTime = dateOrderedItems.First();
            entity.LastDateOfValidityTime = dateOrderedItems.Last();
        }

        public static CollectionMetadataEntity GetMasterCollectionEntity(GeometryFactory geometryFactory,
            Envelope bounds, IList<ItemEntity> items, ShapeGeometryType typeShape, string title, string collectionId)
        {
            var dateOrderedItems = items.OrderBy(x => x.ValidityTime);
            var entity = new CollectionMetadataEntity
            {
                Title = title,
                CollectionId = collectionId,
                AccessType = AccessType.Private,
                StoreType = StoreType.Community,
                Type = GetCollectionType(typeShape),
                Extent = new Polygon(new LinearRing(VectorHelper.GetCoordinates(bounds)), geometryFactory),
                FeatureCount = items.Count,
                EwocCodes = items.Select(c => c.EwocCode).Distinct().ToArray(),
                //LandCovers = items.Select(c => c.Lc).Distinct().ToArray(),
                IrrTypes = items.Select(c => c.IrrigationStatus).Distinct().ToArray(),
                FirstDateOfValidityTime = dateOrderedItems.First().ValidityTime,
                LastDateOfValidityTime = dateOrderedItems.Last().ValidityTime,
                AdditionalData = string.Empty,
            };
            return entity;
        }

        public static CollectionMetadataEntity UpdateMasterCollectionEntity(CollectionMetadataEntity entity,
            GeometryFactory geometryFactory,
            Envelope bounds, IList<ItemEntity> items, ShapeGeometryType typeShape, string title,
            TypeOfObservationMethod typeOfObservationMethod, string collectionId)
        {
            var dateOrderedItems = items.OrderBy(x => x.ValidityTime);
            entity.Title = title;
            entity.CollectionId = collectionId;
            entity.AccessType = AccessType.Private;
            entity.StoreType = StoreType.Community;
            entity.Type = GetCollectionType(typeShape);
            entity.Extent = new Polygon(new LinearRing(VectorHelper.GetCoordinates(bounds)), geometryFactory);
            entity.FeatureCount = items.Count;
            entity.EwocCodes = items.Select(c => c.EwocCode).Distinct().ToArray();
            //entity.LandCovers = items.Select(c => c.Lc).Distinct().ToArray();
            entity.IrrTypes = items.Select(c => c.IrrigationStatus).Distinct().ToArray();
            entity.FirstDateOfValidityTime = dateOrderedItems.First().ValidityTime;
            entity.LastDateOfValidityTime = dateOrderedItems.Last().ValidityTime;
            return entity;
        }

        private static CollectionType GetCollectionType(ShapeGeometryType typeShape)
        {
            if (typeShape == ShapeGeometryType.Point)
            {
                return CollectionType.Point;
            }

            if (typeShape == ShapeGeometryType.Polygon)
            {
                return CollectionType.Polygon;
            }

            return CollectionType.ClassifiedMap;
        }
    }
}