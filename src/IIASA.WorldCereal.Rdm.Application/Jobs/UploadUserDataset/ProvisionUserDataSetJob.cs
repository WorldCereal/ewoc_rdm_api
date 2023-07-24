using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Core;
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
        private readonly IAddCollectionHelper _addCollectionHelper;
        private readonly ILogger<ProvisionUserDataSetJob> _logger;

        public ProvisionUserDataSetJob(IRepository<UserDataset, Guid> userDatasetRepository,
            IBackgroundJobManager backgroundJobManager,
            IAddCollectionHelper addCollectionHelper, ILogger<ProvisionUserDataSetJob> logger)
        {
            _userDatasetRepository = userDatasetRepository;
            _backgroundJobManager = backgroundJobManager;
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
                    out var items, userDataset.CollectionId, args.Path);

                var masterCollectionStore =
                    await _addCollectionHelper.GetMasterCollectionStoreInfo(userDataset.CollectionId);

                if (masterCollectionStore == default)
                {
                    CollectionMetadataEntity masterCollectionStoreInfo =
                        GetMasterCollectionEntity(geometryFactory, bounds, items, typeShape, userDataset.Title,
                            userDataset.TypeOfObservationMethod, userDataset.CollectionId);

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
                userDataset.Errors = new[] {e.ToString()};
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
                UserDatasetId = args.UserDatasetId, Path = args.Path, DirectoryPathToClean = args.DirectoryPathToClean,
                ZipFilePath = args.ZipFilePath
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
            entity.TypeOfObservationMethod = userDataset.TypeOfObservationMethod;
            entity.CollectionId = userDataset.CollectionId;
            entity.Type = GetCollectionType(typeShape);
            entity.Extent.EnvelopeInternal.ExpandToInclude(
                new Polygon(new LinearRing(GetCoordinates(bounds)), geometryFactory).EnvelopeInternal);
            entity.FeatureCount = entity.FeatureCount + items.Count;
            entity.CropTypes = entity.CropTypes.Concat(items.Select(c => c.Ct)).Distinct().ToArray();
            entity.LandCovers = entity.LandCovers.Concat(items.Select(c => c.Lc)).Distinct().ToArray();
            entity.IrrTypes = entity.IrrTypes.Concat(items.Select(c => c.Irr)).Distinct().ToArray();
            entity.FirstDateOfValidityTime = dateOrderedItems.First();
            entity.LastDateOfValidityTime = dateOrderedItems.Last();
        }

        public static CollectionMetadataEntity GetMasterCollectionEntity(GeometryFactory geometryFactory,
            Envelope bounds, IList<ItemEntity> items, ShapeGeometryType typeShape, string title,
            TypeOfObservationMethod typeOfObservationMethod, string collectionId)
        {
            var dateOrderedItems = items.OrderBy(x => x.ValidityTime);
            var entity = new CollectionMetadataEntity
            {
                Title = title,
                TypeOfObservationMethod = typeOfObservationMethod,
                CollectionId = collectionId,
                AccessType = AccessType.Private,
                StoreType = StoreType.Community,
                Type = GetCollectionType(typeShape),
                Extent = new Polygon(new LinearRing(GetCoordinates(bounds)), geometryFactory),
                FeatureCount = items.Count,
                CropTypes = items.Select(c => c.Ct).Distinct().ToArray(),
                LandCovers = items.Select(c => c.Lc).Distinct().ToArray(),
                IrrTypes = items.Select(c => c.Irr).Distinct().ToArray(),
                FirstDateOfValidityTime = dateOrderedItems.First().ValidityTime,
                LastDateOfValidityTime = dateOrderedItems.Last().ValidityTime
            };
            return entity;
        }
        
        public static CollectionMetadataEntity UpdateMasterCollectionEntity(CollectionMetadataEntity entity,GeometryFactory geometryFactory,
            Envelope bounds, IList<ItemEntity> items, ShapeGeometryType typeShape, string title,
            TypeOfObservationMethod typeOfObservationMethod, string collectionId)
        {
            var dateOrderedItems = items.OrderBy(x => x.ValidityTime);
            entity.Title = title;
            entity.TypeOfObservationMethod = typeOfObservationMethod;
            entity.CollectionId = collectionId;
            entity.AccessType = AccessType.Private;
            entity.StoreType = StoreType.Community;
            entity.Type = GetCollectionType(typeShape);
            entity.Extent = new Polygon(new LinearRing(GetCoordinates(bounds)), geometryFactory);
            entity.FeatureCount = items.Count;
            entity.CropTypes = items.Select(c => c.Ct).Distinct().ToArray();
            entity.LandCovers = items.Select(c => c.Lc).Distinct().ToArray();
            entity.IrrTypes = items.Select(c => c.Irr).Distinct().ToArray();
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

        private static Coordinate[] GetCoordinates(Envelope bounds)
        {
            var xmin = bounds.MinX;
            var ymin = bounds.MinY;
            var xmax = bounds.MaxX;
            var ymax = bounds.MaxY;

            return new Coordinate[]
            {
                new() {X = xmin, Y = ymin},
                new() {X = xmax, Y = ymin},
                new() {X = xmax, Y = ymax},
                new() {X = xmin, Y = ymax},
                new() {X = xmin, Y = ymin}
            };
        }
    }
}