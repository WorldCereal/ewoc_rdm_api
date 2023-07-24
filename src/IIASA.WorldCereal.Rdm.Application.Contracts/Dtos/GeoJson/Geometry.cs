using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IIASA.WorldCereal.Rdm.Dtos.GeoJson
{

    /// <summary>
    /// 
    /// </summary>
    [DataContract]
    public class Geometry
    {
        /// <summary>
        /// Gets or Sets coordinates
        /// </summary>
        [DataMember(Name = "type", EmitDefaultValue = false)]
        [JsonProperty(PropertyName = "type")]
        public string Type { get; set; }

        /// <summary>
        /// Gets or Sets coordinates
        /// </summary>
        [DataMember(Name = "coordinates", EmitDefaultValue = false)]
        [JsonProperty(PropertyName = "coordinates")]
        public JArray Coordinates { get; set; }
    }
}
