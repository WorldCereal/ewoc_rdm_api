namespace IIASA.WorldCereal.Rdm;

public class AzureConfig
{
    public string StorageConnectionStrings { get; set; }
    public string ContainerNameForPublic { get; set; }
    public string ContainerNameForPrivate { get; set; }
    public string UserDatasetContainerName { get; set; }
    public int UserDatasetSASTime { get; set; }
}