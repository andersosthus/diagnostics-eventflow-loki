using Microsoft.Extensions.Configuration;
using Validation;

namespace Microsoft.Diagnostics.EventFlow.Outputs.Loki
{
    public class LokiOutputFactory : IPipelineItemFactory<LokiOutput>
    {
        public LokiOutput CreateItem(IConfiguration configuration, IHealthReporter healthReporter)
        {
            Requires.NotNull(configuration, nameof(configuration));
            Requires.NotNull(healthReporter, nameof(healthReporter));

            return new LokiOutput(configuration, healthReporter);
        }
    }
}
