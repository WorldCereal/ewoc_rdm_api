using System.Text.Json.Serialization;

namespace IIASA.WorldCereal.Rdm.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AccessType
    {
        Public,
        Private,
        PrivateShared, // shared with consortium, but still cannot be used un till verified.
        PrivateSharedVerified, // verified by consortium and good to be used for mode training internally by processing module.
    }

    public enum UserRole
    {
        User,
        Moderator, //should have access to all datasets, EXCEPT for private datasets uploaded by users other than the account itself(Public,PrivateShared,PrivateSharedVerified). This account can be shared between IIASA, WENR and VITO.
        Admin, // full access role to all datasets in RDM for developer only. (Public,PrivateShared,PrivateSharedVerified and also private)
        ElevatedUser, //Role for processing module (VITO) and should have access to: (Public,PrivateSharedVerified, public and private consortium)
    }
}