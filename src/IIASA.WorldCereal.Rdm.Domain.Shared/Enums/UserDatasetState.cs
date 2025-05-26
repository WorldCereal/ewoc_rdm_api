using System.Text.Json.Serialization;

namespace IIASA.WorldCereal.Rdm.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum UserDatasetState
    {
        UploadedColumnExtractionInProgressWait,
        UploadedColumnExtracted,
        UploadedColumnExtractFailed,
        MappingInProgressWait,
        ColumnMappingExtracted,
        ColumnMappingExtractFailed,
        UploadedHarmonisationInProgressWait,
        UploadedValidationInProgressWait,
        ValidationSuccessfulProvisionInProgressWait,
        ValidationFailedUploadRequired,
        StoreProvisioned,
        StoreProvisionFailed,
        AvailableInModule,
		ItemsUpdateInprogress,
        ItemsUpdateFailed,
        PrivateSharedWithConsortium,
        PrivateSharedWithConsortiumVerified,
		PublicDataset
    }
}