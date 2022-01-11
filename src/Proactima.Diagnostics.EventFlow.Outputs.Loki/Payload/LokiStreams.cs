using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.EventFlow.Outputs.Loki
{
    public class LokiStreams
    {
        [JsonProperty(PropertyName = "streams")]
        public List<LokiStream> Streams { get; set; }
    }

    public class LokiStream
    {
        [JsonProperty(PropertyName = "stream")]
        public Dictionary<string, string> Stream { get; set; }

        [JsonProperty(PropertyName = "values")]
        public List<string[]> Values { get; set; }
    }
}
