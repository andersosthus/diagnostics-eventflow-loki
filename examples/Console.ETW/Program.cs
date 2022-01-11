using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Console.ETW
{
    internal sealed class Program
    {
        private static async Task Main(string[] args)
        {
            await Host
                .CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Emitter>();
                    services.AddHostedService<Consumer>();
                })
                .RunConsoleAsync();
        }
    }

}