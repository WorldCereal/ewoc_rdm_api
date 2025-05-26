using System.IO;
using System.Linq;

namespace IIASA.WorldCereal.Rdm.ExcelOps
{
    public static class ExcelMarkers
    {
        static ExcelMarkers()
        {
            AllowedMetadataAttributes =
                File.ReadAllLines(GetFile("ExcelMetadataItemsV1.txt")).Select(x => x.Trim()).ToArray();
            DatasetCollectionId = GetString("ReferenceCuratedDataSet:NameCuratedDataSet");
            DatasetTitle = GetString("CuratedDataSet::TitleCuratedDataSet:");
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
            ReferenceToLicense = GetString("ReferenceToLicense");
            RequiredCitation = GetString("RequiredCitation");

            ProviderCode = GetString("Provider:Code:");
            ProviderDescriptionCuratedDataSet = GetString("Provider:DescriptionCuratedDataSet:");
            ProviderUrl = GetString("Provider:URL:");
            ObservationCoordinateSystem = GetString("Observation::CoordinateSystem");
            RequiredDoi = GetString("OriginalDataSet:DOI");
            Objective = GetString("OriginalDataSet:Objective::");
            Country = GetString("CuratedDataSet:ObservationCuratedDataset:Geometry:Country");
            ProjectCode = GetString("OriginalDataSet:Provider:Code:");
            OriginalDatasetLanguage = GetString("OriginalDataSet:Language::");
            OriginalDatasetName = GetString("OriginalDataSet:NameDataSet");
            ProviderContact = GetString("OriginalDataSet:Provider:Contact:");
            ProviderUserName = GetString("OriginalDataSet:Provider:UserName");
            OriginalDatasetCoordinateSystem = GetString("OriginalDataSet:Observation::CoordinateSystem");

            DatasetDownloadCount = GetString("CuratedDataSet:Downloads");
            DatasetSampleDownloadCount = GetString("CuratedDataSet:Sample:Downloads");
            DatasetMetadataDownloadCount = GetString("CuratedDataSet:Metadata:Downloads");
            DatasetHarmonizationPdf = GetString("CuratedDataSet:Harmonization:Pdf");
            DatasetHarmonizationXl = GetString("CuratedDataSet:Harmonization:Xl");


            AllowedHarmonizationAttributes = File.ReadAllLines(GetFile("HarmonizationExcelCodesV1.txt"))
                .Select(x => x.Trim()).ToArray();
            HarmonizationTitle = GetHarmonizationString("Harmonization:Title");
            HarmonizationColId = GetHarmonizationString("Harmonization:ColId");
            FilteringExplain = GetHarmonizationString("Filtering:Explain:");
            GeometryTransformationExplain = GetHarmonizationString("GeometryTransformation:Explain:");
            SpatialAccuracyExplain = GetHarmonizationString("SpatialAccuracy:Explain:");
            ConfidenceScoreExplain = GetHarmonizationString("ConfidenceScore:Explain:");
            CropTypeExplain = GetHarmonizationString("CropType:Explain:");
            IrrigationStatusExplain = GetHarmonizationString("Irr:Explain:");
            FileName = GetHarmonizationString("FileName");
            DateUploaded = GetHarmonizationString("DateUpload:");
            UploadedBy = GetHarmonizationString("UploadedBy:");
            UploadedByContact = GetHarmonizationString("UploadedBy:Contact");
            DatasetAttributes = GetHarmonizationString("DatasetAttributes:");
            ValDateAttribute = GetHarmonizationString("ObsDate:Attribute:");
            ManualValDate = GetHarmonizationString("Obs:ManualDate:");
            ObsExplain = GetHarmonizationString("Obs:Explain:");
            CropTypeAttribute = GetHarmonizationString("CropType:Attribute:");
            IrrStatusAttribute = GetHarmonizationString("IrrigationStatus:Attribute:");
            CropTypeMappings = GetHarmonizationString("CropTypeMappings:");
            IrrStatusMappings = GetHarmonizationString("IrrigationMappings:");
        }

        // Harmonization Metadata items

        public static readonly string HarmonizationTitle;
        public static readonly string HarmonizationColId;
        public static readonly string FilteringExplain;
        public static readonly string GeometryTransformationExplain;
        public static readonly string SpatialAccuracyExplain;
        public static readonly string ConfidenceScoreExplain;
        public static readonly string CropTypeExplain;
        public static readonly string IrrigationStatusExplain;
        public static readonly string FileName;
        public static readonly string DateUploaded;
        public static readonly string UploadedBy;
        public static readonly string UploadedByContact;
        public static readonly string DatasetAttributes;
        public static readonly string ValDateAttribute;
        public static readonly string ManualValDate; 
        public static readonly string ObsExplain; 
        public static readonly string CropTypeAttribute;
        public static readonly string CropTypeMappings;
        public static readonly string IrrStatusMappings;
        public static readonly string IrrStatusAttribute;

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
        public static readonly string ReferenceToLicense;
        public static readonly string RequiredCitation;
        public const string CollectionDownloadUrl = "CollectionDownloadUrl";
        public const string SampleDownloadUrl = "SampleDownloadUrl";
        public const string GeoParquetDownloadUrl = "GeoParquetDownloadUrl";
        public const string OtherFileLink = "link";

        public static string ProviderCode;
        public static string ProviderDescriptionCuratedDataSet;
        public static string ProviderUrl;
        public static string ObservationCoordinateSystem;
        public static readonly string RequiredDoi;
        public static readonly string Objective;
        public static readonly string Country;
        public static readonly string ProjectCode;
        public static readonly string OriginalDatasetName;
        public static readonly string OriginalDatasetLanguage;
        public static readonly string OriginalDatasetCoordinateSystem;
        public static readonly string ProviderContact;
        public static readonly string ProviderUserName;
        public static readonly string DatasetDownloadCount;
        public static readonly string DatasetSampleDownloadCount;
        public static readonly string DatasetMetadataDownloadCount;
        public static readonly string DatasetHarmonizationPdf;
        public static readonly string DatasetHarmonizationXl;

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
            NoOfObservations
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
            NoOfObservations
        };

        public static readonly string[] AllowedMetadataAttributes;

        private static string GetString(string name)
        {
            return AllowedMetadataAttributes.FirstOrDefault(x => x.Contains(name));
        }

        public static readonly string[] AllowedHarmonizationAttributes;

        private static string GetHarmonizationString(string name)
        {
            return AllowedHarmonizationAttributes.FirstOrDefault(x => x.Contains(name));
        }
    }
}