using System.Collections.Generic;
using NetTopologySuite.Geometries;

namespace IIASA.WorldCereal.Rdm.Core
{
    public interface ICollectionSearchHelper
    {
        string[] GetCollectionIds(int? startIndex, int? limit, List<decimal?> bbox, string datetime, string filter);

        Geometry GetGeometry(List<decimal?> bbox);
    }
}