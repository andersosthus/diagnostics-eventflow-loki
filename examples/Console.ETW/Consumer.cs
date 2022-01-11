using Microsoft.Diagnostics.EventFlow;
using Microsoft.Extensions.Hosting;

namespace Console.ETW
{
    internal class Consumer : IHostedService
    {
        private readonly IHostApplicationLifetime _appLifetime;
        private DiagnosticPipeline _pipeline;

        public Consumer(IHostApplicationLifetime appLifetime)
        {
            _appLifetime = appLifetime;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _appLifetime.ApplicationStarted.Register(() =>
            {
                Task.Run(async () =>
                {
                    _pipeline = DiagnosticPipelineFactory.CreatePipeline("eventFlowConfig.json");
                });
            });

            _appLifetime.ApplicationStopping.Register(() =>
            {
                _pipeline?.Dispose();
            });

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _pipeline?.Dispose();

            return Task.CompletedTask;
        }
    }
}
