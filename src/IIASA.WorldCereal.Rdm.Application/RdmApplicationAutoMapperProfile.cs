using AutoMapper;
using IIASA.WorldCereal.Rdm.Dtos;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.Mappings;

namespace IIASA.WorldCereal.Rdm
{
    public class RdmApplicationAutoMapperProfile : Profile
    {
        public RdmApplicationAutoMapperProfile()

        {
            /* You can configure your AutoMapper mapping configuration here.
             * Alternatively, you can split your mapping configurations
             * into multiple profile classes for a better organization. */

            CreateMap<StoreDto, StoreEntity>().ReverseMap();

            CreateMap<AddStoreDto, StoreEntity>()
                .ForMember(x => x.StoreType, m => m.MapFrom(x => x.StoreType))
                .ForMember(x => x.ConnectionString, m => m.MapFrom(x => x.ConnectionString))
                .ForMember(x => x.Name, m => m.MapFrom(x => x.Name))
                .ForAllOtherMembers(x => x.Ignore());

            CreateMap<CollectionMetadataEntity, MasterCollectionStoreInfoDto>()
                .ForMember(x => x.BoundingBoxPoints,
                    m => m.MapFrom(x => SpatialMappingHelper.GetCoordinates(x.Extent.Coordinates)));

            CreateMap<AddMasterCollectionStoreInfoDto, CollectionMetadataEntity>()
                .ForMember(x => x.Extent, m => m.MapFrom(x => SpatialMappingHelper.GetGeometry(x.BoundingBoxPoints)))
                .ForMember(x => x.TenantId, y => y.Ignore())
                .ForMember(x => x.LastModificationTime, y => y.Ignore())
                .ForMember(x => x.LastModifierId, y => y.Ignore())
                .ForMember(x => x.CreationTime, y => y.Ignore())
                .ForMember(x => x.CreatorId, y => y.Ignore())
                .ForMember(x => x.Id, y => y.Ignore());


            CreateMap<CollectionMetadataEntity, CollectionMetadataDto>()
                .ForMember(x => x.Crs, x => x.Ignore())
                .ForMember(x => x.Extent,
                    m => m.MapFrom(x =>
                        SpatialMappingHelper.GetExtent(x.Extent, x.FirstDateOfValidityTime, x.LastDateOfValidityTime)));


            CreateMap<CollectionMetadataEntity, CollectionSummaryDto>()
                .ForMember(x => x.CollectionId, m => m.MapFrom(x => x.CollectionId))
                .ForMember(x => x.Type, m => m.MapFrom(x => x.Type))
                .ForMember(x => x.Crs, m => m.Ignore())
                .ForMember(x => x.Extent,
                    m => m.MapFrom(x =>
                        SpatialMappingHelper.GetExtent(x.Extent, x.FirstDateOfValidityTime, x.LastDateOfValidityTime)));

            CreateMap<CollectionMetadataEntity, CollectionMetadataEntity>()
                .ForMember(x => x.Id, x => x.Ignore());


            CreateMap<UserDataset, UserDatasetViewModel>();
            CreateMap<MetadataItem, MetadataItemDto>();
            CreateMap<MetadataItemDto, MetadataItem>()
                .ForMember(x => x.Id, x => x.Ignore())
                .ForMember(x => x.LastModificationTime, x => x.Ignore())
                .ForMember(x => x.LastModifierId, x => x.Ignore())
                .ForMember(x => x.CreationTime, x => x.Ignore())
                .ForMember(x => x.CreatorId, x => x.Ignore())
                .ForMember(x => x.TenantId, x => x.Ignore());

            CreateMap<DatasetEvent, DatasetEventViewModel>().ForMember(x => x.CanSubmit, x => x.Ignore());

            CreateMap<ConsortiumCollectionDto, ConsortiumCollection>().ReverseMap();
        }
    }
}