using System;
using Volo.Abp.BackgroundJobs;

namespace IIASA.WorldCereal.Rdm.Jobs.UploadUserDataset
{
    [BackgroundJobName("Provision User dataset job")]
    public class ProvisionUserDataSetJobArgs
    {
        public Guid UserDatasetId { get; set; }

        public string Path { get; set; }

        public string DirectoryPathToClean { get; set; }
        
        public string ZipFilePath { get; set; }
    }
}
