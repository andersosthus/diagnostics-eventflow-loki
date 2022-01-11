using Microsoft.Extensions.Hosting;

namespace Console.ETW
{
    internal class Emitter : IHostedService
    {
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly Random _random;

        public Emitter(IHostApplicationLifetime appLifetime)
        {
            _appLifetime = appLifetime;
            _random = Random.Shared;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _appLifetime.ApplicationStarted.Register(() =>
            {
                Task.Run(async () =>
                {
                    //do
                    //{
                    //    var count = _random.Next(0, 10);
                    //    for (var i = 0; i < count; i++)
                    //    {
                    //        ETWSource.Log.TestEvent($"generated {count} messages - this is #{i}");
                    //    }

                    //    await Task.Delay(TimeSpan.FromSeconds(2));
                    //} while (true);

                    await Task.Delay(TimeSpan.FromSeconds(2));
                    ETWSource.Log.TestEvent($"generated message");
                });
            });

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
