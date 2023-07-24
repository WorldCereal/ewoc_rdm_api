using System.Collections.Generic;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Entity;
using NetTopologySuite.IO;

namespace IIASA.WorldCereal.Rdm.Jobs.UploadUserDataset
{
    public interface IShapeFileUpdater
    {
        ShapeGeometryType ShapeGeometryType { get; set; }

        IList<ItemEntity> ExtractItemsToSave(VectorFileReader reader, string collectionId);
    }
}
