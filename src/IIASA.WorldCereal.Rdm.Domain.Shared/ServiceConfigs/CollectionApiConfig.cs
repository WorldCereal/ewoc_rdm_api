namespace IIASA.WorldCereal.Rdm.ServiceConfigs
{
    public class CollectionApiConfig
    {
        public int DefaultItemsLimit { get; set; } = 10;
    }

    public class UserDatasetConfig
    {
        public string TempFolder { get; set; }

        public string DatasetBackupFolder { get; set; }
    }

    public class EwocConfig
    {
        public string UserIdKey { get; set; }
        public string UserGroupKey { get; set; }
        public string UserNameKey { get; set; }

        public string UserInfo { get; set; }

        public string[] AdminGroupNames { get; set; }

        public bool AuthEnabled { get; set; }
    }
}