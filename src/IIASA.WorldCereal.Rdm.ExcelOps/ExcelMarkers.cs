using System.IO;
using System.Linq;

namespace IIASA.WorldCereal.Rdm.ExcelOps
{
    public static class ExcelMarkers
    {
        static ExcelMarkers()
        {
            AllowedAttributes = File.ReadAllLines(GetFile("ExcelMetadataItemsV1.txt")).Select(x => x.Trim()).ToArray();
            DatasetCollectionId = GetString("ReferenceCuratedDataSet:NameCuratedDataSet");
            DatasetTitle = GetString("TitleCuratedDataSet");
            DatasetDescription = GetString("DescriptionCuratedDataSet");
            ConfidenceLandCover = GetString("ConfidenceLandCover");
            ConfidenceCropType = GetString("ConfidenceCropType");
            ConfidenceIrrigation = GetString("ConfidenceIrrigationRainfed");
            GeometryPointOrPolygonOrRaster = GetString("PointOrPolygonOrRaster");
            GeometryBoundingBoxLl = GetString("BoundingBoxLL");
            GeometryBoundingBoxUr = GetString("BoundingBoxUR");
            FirstDateObservation = GetString("FirstDateObservation");
            LastDateObservation = GetString("LastDateObservation");
            ListLandCovers = GetString("ListOfLandCovers");
            ListOfCropTypes = GetString("ListOfCropTypes");
            ListOfIrrigationCodes = GetString("ListOfIrrigationCodes");
            NoOfObservations = GetString("NoOfObservations");
            TypeOfObservationMethod = GetString("TypeOfObservationMethod");
            TypeOfLicenseAccessType = GetString("TypeOfLicense");
        }

        public static string GetFile(string fileName)
        {
            var files = Directory.GetFiles(Directory.GetCurrentDirectory(), fileName,
                SearchOption.AllDirectories);
            var file = files.First();
            return file;
        }

        //TODO put in db
        public static readonly string DatasetTitle;
        public static readonly string DatasetDescription;
        public static readonly string DatasetCollectionId;
        public static readonly string ConfidenceLandCover;
        public static readonly string ConfidenceCropType;
        public static readonly string ConfidenceIrrigation;
        public static readonly string GeometryPointOrPolygonOrRaster;
        public static readonly string GeometryBoundingBoxLl;
        public static readonly string GeometryBoundingBoxUr;
        public static readonly string FirstDateObservation;
        public static readonly string LastDateObservation;
        public static readonly string ListLandCovers;
        public static readonly string ListOfCropTypes;
        public static readonly string ListOfIrrigationCodes;
        public static readonly string NoOfObservations;
        public static readonly string TypeOfObservationMethod;
        public static readonly string TypeOfLicenseAccessType;
        public static readonly string CollectionDownloadUrl = "CollectionDownloadUrl";

        public static readonly string[] AttributesFromDataset =
        {
            DatasetTitle,
            DatasetCollectionId,
            ConfidenceLandCover,
            ConfidenceCropType,
            ConfidenceIrrigation,
            GeometryPointOrPolygonOrRaster,
            GeometryBoundingBoxLl,
            GeometryBoundingBoxUr,
            FirstDateObservation,
            LastDateObservation,
            ListLandCovers,
            ListOfCropTypes,
            ListOfIrrigationCodes,
            NoOfObservations,
            TypeOfObservationMethod
        };
        
        public static readonly string[] AttributesFromDatasetNoUpdate =
        {
            DatasetTitle,
            DatasetCollectionId,
            GeometryPointOrPolygonOrRaster,
            GeometryBoundingBoxLl,
            GeometryBoundingBoxUr,
            FirstDateObservation,
            LastDateObservation,
            ListLandCovers,
            ListOfCropTypes,
            ListOfIrrigationCodes,
            NoOfObservations,
            TypeOfObservationMethod
        };

        public static readonly string[] AllowedAttributes;

        private static string GetString(string name)
        {
            return AllowedAttributes.FirstOrDefault(x => x.Contains(name));
        }
    }
}