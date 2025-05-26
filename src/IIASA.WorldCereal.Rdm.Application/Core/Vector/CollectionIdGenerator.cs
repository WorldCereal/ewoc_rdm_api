using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IIASA.WorldCereal.Rdm.Entity;
using NetTopologySuite.IO;

namespace IIASA.WorldCereal.Rdm.Core.Vector;

public static class CollectionIdGenerator
{
    public static string GetFileName(string path)
    {
        return Uri.EscapeDataString(Path.GetFileName(path).Split(".")[0].Replace(" ", ""));
    }
    
    public static string GetWktPolygon(IList<ItemEntity> itemsToAdd)
    {
        var bbox = itemsToAdd[0].Geometry.EnvelopeInternal;
        for (int i = 1; i < itemsToAdd.Count; i++)
        {
            var nextGeom = itemsToAdd[i].Geometry.EnvelopeInternal;
            bbox.ExpandToInclude(nextGeom);
        }

        var bboxGeom = itemsToAdd[0].Geometry.Factory.ToGeometry(bbox);
        return bboxGeom.ToString();
    }

    public static string GetInfoConent(IList<ItemEntity> itemsToAdd)
    {
        var lc = itemsToAdd.Any(x => x.EwocCode > 0) ? "1" : "0";
        var crop = itemsToAdd.Any(x => x.EwocCode is > 1000000000 and < 1500000000) ? "1" : "0";
        var irr = itemsToAdd.Any(x => x.IrrigationStatus > 0) ? "1" : "0";
        return lc + crop + irr;
    }
    
    public static string GetColId(string year, string region, string identifier, ShapeGeometryType typeShape, string landInfo)
    {
        var type = $"{typeShape:G}".Replace("Polygon", "Poly");
        if (identifier.Length > 20)
        {
            identifier = identifier[..20];
        }
        return $"{year}_{region.ToUpperInvariant()}_{identifier}_{type}_{landInfo}".ToLowerInvariant();
    }
}