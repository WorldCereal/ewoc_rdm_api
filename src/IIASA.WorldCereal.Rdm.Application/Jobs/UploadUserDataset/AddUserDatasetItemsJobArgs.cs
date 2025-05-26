using System;
using Volo.Abp.BackgroundJobs;

namespace IIASA.WorldCereal.Rdm.Jobs.UploadUserDataset
{
    [BackgroundJobName("Add UserDataset items to store")]
    public class AddUserDatasetItemsJobArgs
    {
        public Guid UserDatasetId { get; set; }

        public string VectorFilePath { get; set; }

        public string DirectoryPathToClean { get; set; }
    }
}