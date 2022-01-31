using FluentAssertions;
using System.Collections.Generic;
using Xunit;

namespace Proactima.Diagnostics.EventFlow.Outputs.Loki.Tests
{
    public class DescribeStreamGrouper
    {
        [Fact]
        public void ItCanGroupOneLabelWithTwoDifferentValues()
        {
            var testEvents = new List<LokiItem>
            {
                new LokiItem {
                    Labels = new Dictionary<string, string> {
                        ["node"] = "nodea",
                    },
                    Payload = new []{ "1", "message-one" }
                },
                new LokiItem {
                    Labels = new Dictionary<string, string> {
                        ["node"] = "nodeb",
                    },
                    Payload = new []{ "2", "message-two" }
                },
                new LokiItem {
                    Labels = new Dictionary<string, string> {
                        ["node"] = "nodea",
                    },
                    Payload = new []{ "3", "message-three" }
                },
                new LokiItem {
                    Labels = new Dictionary<string, string> {
                        ["machine"] = "localhost",
                    },
                    Payload = new []{ "4", "message-four" }
                },
            };

            var actual = StreamGrouper.Process(testEvents, new Dictionary<string, string>());
            actual.Count.Should().Be(3);
        }

        [Fact]
        public void ItCanGroupTwoLabelsWithTwoDifferentValues()
        {
            var testEvents = new List<LokiItem>
            {
                new LokiItem {
                    Labels = new Dictionary<string, string> {
                        ["node"] = "nodea",
                        ["project"] = "myproject",
                    },
                    Payload = new []{ "1", "message-one" }
                },
                new LokiItem {
                    Labels = new Dictionary<string, string> {
                        ["node"] = "nodeb",
                        ["project"] = "otherproject",
                    },
                    Payload = new []{ "2", "message-two" }
                },
                new LokiItem {
                    Labels = new Dictionary<string, string> {
                        ["node"] = "nodea",
                        ["project"] = "otherproject",
                    },
                    Payload = new []{ "3", "message-three" }
                },
                new LokiItem {
                    Labels = new Dictionary<string, string> {
                        ["node"] = "nodec",
                        ["project"] = "secretproject",
                    },
                    Payload = new []{ "4", "message-four" }
                },
            };

            var actual = StreamGrouper.Process(testEvents, new Dictionary<string, string>());
            actual.Count.Should().Be(4);
        }

        [Fact]
        public void ItAppliesStaticLabels()
        {
            var testEvents = new List<LokiItem>
            {
                new LokiItem {
                    Labels = new Dictionary<string, string> {
                        ["node"] = "nodea",
                    },
                    Payload = new []{ "1", "message-one" }
                }
            };

            var staticLables = new Dictionary<string, string>
            {
                ["mylabel"] = "csharp"
            };

            var actual = StreamGrouper.Process(testEvents, staticLables);
            actual.Count.Should().Be(1);
            actual[0].Stream.Count.Should().Be(2);
            actual[0].Stream.Should().ContainKey("mylabel");
        }
    }
}
