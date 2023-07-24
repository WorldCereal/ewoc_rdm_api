using System.Text.Json.Serialization;

namespace IIASA.WorldCereal.Rdm.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum UserDatasetState
    {
        UploadedValidationInProgressWait,
        ValidationSuccessfulProvisionInProgressWait,
        ValidationFailedUploadRequired,
        StoreProvisioned,
        StoreProvisionFailed,
        AvailableInModule,
		ItemsUpdateInprogress,
        ItemsUpdateFailed,
		PublicDataset
    }
}