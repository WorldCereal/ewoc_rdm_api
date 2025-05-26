using System.Collections.Generic;
using IIASA.WorldCereal.Rdm.Core.Vector;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.Enums;
using NetTopologySuite.IO;

namespace IIASA.WorldCereal.Rdm.Jobs.UploadUserDataset
{
    public class ShapeFileUpdater : IShapeFileUpdater
    {
        public ShapeGeometryType ShapeGeometryType { get; set; }
        protected int EwocCodeIndex { get; set; } = -1;
        protected int IrrIndex { get; set; } = -1;
        protected int SampleIdIndex { get; set; } = -1;
        protected int ValTimeIndex { get; set; } = -1;
        protected int ImageryTimeIndex { get; set; } = -1;
        protected int NumberOfValidationsIndex { get; set; } = -1;
        protected int TypeOfValidatorIndex { get; set; } = -1;
        protected int AgreementOfObservationsIndex { get; set; } = -1;
        protected int DisAgreementOfObservationsIndex { get; set; } = -1;
        protected int H3Index { get; set; } = -1;
        protected int ExtractIndex { get; set; } = -1;

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

        private void InitializeIndexes(VectorFileReader vectorFileReader)
        {
            EwocCodeIndex = vectorFileReader.GetOrdinalExt(AttributeNames.EwocCode);
            IrrIndex = vectorFileReader.GetOrdinalExt(AttributeNames.Irrigation);
            SampleIdIndex = vectorFileReader.GetOrdinalExt(AttributeNames.SampleId);
            ValTimeIndex = vectorFileReader.GetOrdinalExt(AttributeNames.ValidityTime);
            ImageryTimeIndex = vectorFileReader.GetOrdinalExt(AttributeNames.ImageryTime);
            NumberOfValidationsIndex = vectorFileReader.GetOrdinalExt(AttributeNames.NumberOfValidations);
            TypeOfValidatorIndex = vectorFileReader.GetOrdinalExt(AttributeNames.TypeOfValidator);
            AgreementOfObservationsIndex = vectorFileReader.GetOrdinalExt(AttributeNames.AgreementOfObservations);
            DisAgreementOfObservationsIndex = vectorFileReader.GetOrdinalExt(AttributeNames.DisagreementOfObservations);
            H3Index = vectorFileReader.GetOrdinalExt(AttributeNames.H3Index);
            ExtractIndex = vectorFileReader.GetOrdinalExt(AttributeNames.Extract);
        }

        private ItemEntity GetEntity(VectorFileReader vectorFileReader, string collectionId)
        {
            return new ItemEntity
            {
                CollectionId = collectionId,
                EwocCode = vectorFileReader.GetInt64Ext(EwocCodeIndex),
                IrrigationStatus = vectorFileReader.GetInt32Ext(IrrIndex),
                SampleId = vectorFileReader.GetStringExt(SampleIdIndex),
                ValidityTime = vectorFileReader.GetDateTimeExt(ValTimeIndex),
                Geometry = vectorFileReader.Geometry,

                //optional
                ImageryDate = ImageryTimeIndex > 0 ? vectorFileReader.GetDateTimeExt(ImageryTimeIndex) : null,
                NumberOfValidations =
                    NumberOfValidationsIndex > 0 ? vectorFileReader.GetInt32Ext(NumberOfValidationsIndex) : null,
                TypeOfValidator = TypeOfValidatorIndex > 0
                    ? (ValidatorType) vectorFileReader.GetInt32Ext(TypeOfValidatorIndex)
                    : null,
                AgreementOfObservations = AgreementOfObservationsIndex > 0
                    ? vectorFileReader.GetInt32Ext(AgreementOfObservationsIndex)
                    : null,
                DisAgreementOfObservations = DisAgreementOfObservationsIndex > 0
                    ? vectorFileReader.GetInt32Ext(DisAgreementOfObservationsIndex)
                    : null,
                H3Index = H3Index > 0
                    ? vectorFileReader.GetStringExt(H3Index)
                    : string.Empty,
                Extract = vectorFileReader.GetInt32Ext(ExtractIndex),
            };
        }

    }
}