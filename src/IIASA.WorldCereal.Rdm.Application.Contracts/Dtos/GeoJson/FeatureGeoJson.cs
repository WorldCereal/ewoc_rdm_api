using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace IIASA.WorldCereal.Rdm.Dtos.GeoJson {

  /// <summary>
  /// 
  /// </summary>
  [DataContract]
  public class FeatureGeoJson {
    /// <summary>
    /// Gets or Sets Type
    /// </summary>
    [DataMember(Name="type", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "type")]
    public string Type { get; set; }

    /// <summary>
    /// Gets or Sets Geometry
    /// </summary>
    [DataMember(Name="geometry", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "geometry")]
    public Geometry Geometry { get; set; }

    /// <summary>
    /// Gets or Sets Properties
    /// </summary>
    [DataMember(Name="properties", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "properties")]
    public object Properties { get; set; }

    /// <summary>
    /// Gets or Sets Sample ID, 
    /// </summary>
    [DataMember(Name="id", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "id")]
    public string Id { get; set; }

  }
}
