// Ruta: SimpleExtractor/Json/JsonVisualization.cs
using Newtonsoft.Json;
using System.Collections.Generic;

namespace SimpleExtractor.Json
{
    public class JsonVisualizationData
    {
        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("1", NullValueHandling = NullValueHandling.Ignore)]
        public JsonVisualization? Size1 { get; set; }

        [JsonProperty("32", NullValueHandling = NullValueHandling.Ignore)]
        public JsonVisualization? Size32 { get; set; }

        [JsonProperty("64", NullValueHandling = NullValueHandling.Ignore)]
        public JsonVisualization? Size64 { get; set; }
    }

    public class JsonVisualization
    {
        [JsonProperty("size")]
        public int Size { get; set; }

        [JsonProperty("layerCount")]
        public int LayerCount { get; set; }

        [JsonProperty("angle")]
        public int Angle { get; set; }

        [JsonProperty("layers")]
        public Dictionary<string, JsonLayer>? Layers { get; set; }

        [JsonProperty("directions")]
        public Dictionary<string, object>? Directions { get; set; }

        [JsonProperty("colors", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, List<JsonColor>>? Colors { get; set; }

        [JsonProperty("animations", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, JsonAnimation>? Animations { get; set; }
    }

    public class JsonLayer
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("z", NullValueHandling = NullValueHandling.Ignore)]
        public int? Z { get; set; }

        [JsonProperty("ink", NullValueHandling = NullValueHandling.Ignore)]
        public string? Ink { get; set; }

        [JsonProperty("alpha", NullValueHandling = NullValueHandling.Ignore)]
        public int? Alpha { get; set; }

        [JsonProperty("ignoreMouse", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IgnoreMouse { get; set; }
    }

    public class JsonColor
    {
        [JsonProperty("layerId")]
        public int LayerId { get; set; }

        [JsonProperty("color")]
        public string? Color { get; set; }
    }

    public class JsonAnimation
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("layers")]
        public Dictionary<string, JsonAnimationLayer>? Layers { get; set; }
    }

    public class JsonAnimationLayer
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("frameRepeat", NullValueHandling = NullValueHandling.Ignore)]
        public int? FrameRepeat { get; set; }

        [JsonProperty("loopCount", NullValueHandling = NullValueHandling.Ignore)]
        public int? LoopCount { get; set; }

        [JsonProperty("frames")]
        public List<int>? Frames { get; set; }
    }
}