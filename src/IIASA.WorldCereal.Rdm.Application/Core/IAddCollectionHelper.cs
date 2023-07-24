using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Entity;
using Volo.Abp.Uow;

namespace IIASA.WorldCereal.Rdm.Core
{
    public interface IAddCollectionHelper
    {
        Task<CollectionMetadataEntity> CreateMasterCollectionStoreInfo(CollectionMetadataEntity addMasterCollectionStoreInfoDto,IUnitOfWork currentUnitOfWork);

        Task<CollectionMetadataEntity> CreateMasterCollectionStoreInfo(CollectionMetadataEntity masterCollectionStoreInfo);

        Task<CollectionMetadataEntity> GetMasterCollectionStoreInfo(string collectionId);

        Task SaveMasterCollectionStoreInfoAsync(CollectionMetadataEntity data);

        Task DeleteUserDataset(string collectionId);
    }
}