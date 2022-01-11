using System.Collections.Generic;

namespace Microsoft.Diagnostics.EventFlow.Outputs.Loki
{
    public struct LokiItem
    {
        public string[] Payload { get; set; }
        public Dictionary<string, string> Labels { get; set; }
    }

    public struct LokiPayload
    {
        public string TimeStamp { get; set; }
        public string Message { get; set; }
    }
}
