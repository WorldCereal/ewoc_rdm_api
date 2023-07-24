using System;
using Volo.Abp.BackgroundJobs;

namespace IIASA.WorldCereal.Rdm.Jobs.UploadUserDataset
{
    [BackgroundJobName("Add UserDataset items to store")]
    public class AddUserDatasetItemsJobArgs
    {
        public Guid UserDatasetId { get; set; }

        public string Path { get; set; }

        public string DirectoryPathToClean { get; set; }
        
        public string ZipFilePath { get; set; }
    }
}