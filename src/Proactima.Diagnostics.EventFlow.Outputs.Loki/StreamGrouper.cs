using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Proactima.Diagnostics.EventFlow.Outputs.Loki
{
    public static class StreamGrouper
    {
        public static List<LokiStream> Process(List<LokiItem> items, Dictionary<string, string> staticLabels)
        {
            var tracker = new Dictionary<string, LokiStream>();
            var sb = new StringBuilder();

            foreach (var item in items)
            {
                sb.Clear();
                foreach (var labelPair in item.Labels)
                {
                    sb.Append(labelPair.Key);
                    sb.Append(labelPair.Value);
                }

                var trackingKey = sb.ToString();
                if (tracker.ContainsKey(trackingKey))
                {
                    tracker[trackingKey].Values.Add(item.Payload);
                }
                else
                {
                    var labels = new Dictionary<string, string>(item.Labels);
                    foreach(var kvp in staticLabels)
                    {
                        labels[kvp.Key] = kvp.Value;
                    }

                    var streamItem = new LokiStream
                    {
                        Stream = labels,
                        Values = new List<string[]> { item.Payload },
                    };
                    tracker[trackingKey] = streamItem;
                }
            }

            return tracker.Select(x => x.Value).ToList();
        }
    }
}
