// Ruta: SimpleExtractor/Json/JsonFurniData.cs
using Newtonsoft.Json;
using System.Collections.Generic;

namespace SimpleExtractor.Json
{
    public class JsonFurniData
    {
        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("visualization")]
        public JsonVisualizationData? Visualization { get; set; }

        [JsonProperty("logic")]
        public JsonLogicData? Logic { get; set; }

        [JsonProperty("assets")]
        public Dictionary<string, JsonAsset>? Assets { get; set; }
    }

    public class JsonLogicData
    {
        [JsonProperty("type")]
        public string? Type { get; set; }

        // <-- CAMBIO: De string a object para permitir números y floats (ej: 2.0)
        [JsonProperty("dimensions")]
        public Dictionary<string, object>? Dimensions { get; set; }

        // <-- CAMBIO: De List<string> a List<int>
        [JsonProperty("directions")]
        public List<int>? Directions { get; set; }
    }

    public class JsonAsset
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("x")]
        public int X { get; set; }

        [JsonProperty("y")]
        public int Y { get; set; }

        [JsonProperty("source", NullValueHandling = NullValueHandling.Ignore)]
        public string? Source { get; set; }

        [JsonProperty("flipH", NullValueHandling = NullValueHandling.Ignore)]
        public bool? FlipH { get; set; }
    }
}