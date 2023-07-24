using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace IIASA.WorldCereal.Rdm.Dtos.GeoJson {

  /// <summary>
  /// 
  /// </summary>
  [DataContract]
  public class FeatureCollectionGeoJSON {
    /// <summary>
    /// Gets or Sets Type
    /// </summary>
    [DataMember(Name="type", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "type")]
    public string Type { get; set; }

    /// <summary>
    /// Gets or Sets Features
    /// </summary>
    [DataMember(Name="features", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "features")]
    public List<FeatureGeoJson> Features { get; set; }

  }
}
