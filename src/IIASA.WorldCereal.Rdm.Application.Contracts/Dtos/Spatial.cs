using System.Collections.Generic;

namespace IIASA.WorldCereal.Rdm.Dtos
{
    public class Spatial
    { 
        public IEnumerable<IEnumerable<double>> Bbox { get; set; }
        public string Crs { get; set; } = "http://www.opengis.net/def/crs/OGC/1.3/CRS84";
    }
}