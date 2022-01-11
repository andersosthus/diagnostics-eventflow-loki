using System.Diagnostics.Tracing;

namespace Console.ETW
{
    [EventSource(Name = "ETWTestSource")]
    public class ETWSource : EventSource
    {
        private static readonly ETWSource Instance = new ETWSource();

        public static ETWSource Log => Instance;

        private const int TestEventId = 200;

        [Event(TestEventId, Message = "My message with body {0}", Level = EventLevel.Informational, Keywords = Keywords.Services)]
        public void TestEvent(string body)
        {
            if(IsEnabled()) WriteEvent(TestEventId, body);
        }

        public class Keywords
        {
            public const EventKeywords Services = (EventKeywords)1;
            public const EventKeywords Search = (EventKeywords)2;
        }
    }
}
