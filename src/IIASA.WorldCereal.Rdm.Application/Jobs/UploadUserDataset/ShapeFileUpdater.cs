using System.Collections.Generic;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.Enums;
using NetTopologySuite.IO;

namespace IIASA.WorldCereal.Rdm.Jobs.UploadUserDataset
{
    public class ShapeFileUpdater : IShapeFileUpdater
    {
        public ShapeGeometryType ShapeGeometryType { get; set; }

        protected int LcIndex { get; set; } = -1;
        protected int CtIndex { get; set; } = -1;
        protected int IrrIndex { get; set; } = -1;
        protected int AreaIndex { get; set; } = -1;
        protected int UserConfIndex { get; set; } = -1;
        protected int SampleIdIndex { get; set; } = -1;
        protected int SplitIndex { get; set; } = -1;
        protected int ValTimeIndex { get; set; } = -1;
        protected int ImageryTimeIndex { get; set; } = -1;
        protected int NumberOfValidationsIndex { get; set; } = -1;
        protected int TypeOfValidatorIndex { get; set; } = -1;
        protected int AgreementOfObservationsIndex { get; set; } = -1;
        protected int DisAgreementOfObservationsIndex { get; set; } = -1;
        protected int SupportingRadiusIndex { get; set; } = -1;

        public IList<ItemEntity> ExtractItemsToSave(VectorFileReader reader, string collectionId)
        {
            InitializeIndexes(reader);

            var items = new List<ItemEntity>();

            while (reader.Read())
            {
                ItemEntity entity = GetEntity(reader, collectionId);
                entity.Geometry.SRID = GeoJsonHelper.GeometryWgs84Srid;
                items.Add(entity);
            }

            return items;
        }

        private void InitializeIndexes(VectorFileReader shpReader)
        {
            LcIndex = shpReader.GetOrdinalExt(AttributeNames.LandCover);
            CtIndex = shpReader.GetOrdinalExt(AttributeNames.CropType);
            IrrIndex = shpReader.GetOrdinalExt(AttributeNames.Irrigation);
            AreaIndex = shpReader.GetOrdinalExt(AttributeNames.Area);
            UserConfIndex = shpReader.GetOrdinalExt(AttributeNames.UserConf);
            SampleIdIndex = shpReader.GetOrdinalExt(AttributeNames.SampleId);
            SplitIndex = shpReader.GetOrdinalExt(AttributeNames.Split);
            ValTimeIndex = shpReader.GetOrdinalExt(AttributeNames.ValidityTime);

            ImageryTimeIndex = shpReader.GetOrdinalExt(AttributeNames.ImageryTime);
            NumberOfValidationsIndex = shpReader.GetOrdinalExt(AttributeNames.NumberOfValidations);
            TypeOfValidatorIndex = shpReader.GetOrdinalExt(AttributeNames.TypeOfValidator);
            AgreementOfObservationsIndex = shpReader.GetOrdinalExt(AttributeNames.AgreementOfObservations);
            DisAgreementOfObservationsIndex = shpReader.GetOrdinalExt(AttributeNames.DisagreementOfObservations);
            SupportingRadiusIndex = shpReader.GetOrdinalExt(AttributeNames.SupportingRadius); //6
        }

        private ItemEntity GetEntity(VectorFileReader shpReader, string collectionId)
        {
            return new()
            {
                CollectionId = collectionId,
                Lc = shpReader.GetInt32Ext(LcIndex),
                Ct = shpReader.GetInt32Ext(CtIndex),
                Irr = shpReader.GetInt32Ext(IrrIndex),
                SampleId = shpReader.GetStringExt(SampleIdIndex),
                ValidityTime = shpReader.GetDateTimeExt(ValTimeIndex),
                Geometry = shpReader.Geometry,

                //optional
                Area = shpReader.GetDoubleExt(AreaIndex),
                UserConf = shpReader.GetInt32Ext(UserConfIndex),
                Split = SplitHelper.Get(shpReader.GetStringExt(SplitIndex)),

                ImageryTime = ImageryTimeIndex > 0 ? shpReader.GetDateTimeExt(ImageryTimeIndex) : null,
                NumberOfValidations =
                    NumberOfValidationsIndex > 0 ? shpReader.GetInt32Ext(NumberOfValidationsIndex) : null,
                TypeOfValidator = TypeOfValidatorIndex > 0
                    ? (ValidatorType) shpReader.GetInt32Ext(TypeOfValidatorIndex)
                    : null,
                AgreementOfObservations = AgreementOfObservationsIndex > 0
                    ? shpReader.GetInt32Ext(AgreementOfObservationsIndex)
                    : null,
                DisAgreementOfObservations = DisAgreementOfObservationsIndex > 0
                    ? shpReader.GetInt32Ext(DisAgreementOfObservationsIndex)
                    : null
            };
        }

    }
}