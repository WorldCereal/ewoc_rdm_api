using IIASA.WorldCereal.Rdm.Enums;

namespace IIASA.WorldCereal.Rdm.Dtos
{
    public class AddStoreDto
    {
        public string ConnectionString { get; set; }

        public StoreType StoreType { get; set; }

        public string Name { get; set; }
    }
}