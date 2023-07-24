﻿using System.Text.Json.Serialization;

namespace IIASA.WorldCereal.Rdm.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CollectionType
    {
        Point,
        Polygon,
        ClassifiedMap
    }
}