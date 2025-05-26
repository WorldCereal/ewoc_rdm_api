using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Core.Vector;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace ConsortiumDbUpdate
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Volo.Abp", LogEventLevel.Warning)
                .MinimumLevel.Override("IIASA.WorldCereal.Rdm", LogEventLevel.Debug)
                .Enrich.FromLogContext()
                .CreateLogger();

            await CreateHostBuilder(args).RunConsoleAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args).ConfigureAppConfiguration((context, builder) =>
                {
                    var env = context.HostingEnvironment;
                    builder.SetBasePath(AppContext.BaseDirectory)
                        .AddJsonFile("appsettings.json", optional: false).AddEnvironmentVariables();

                    var configuration = builder.Build();
                    var connectionString = configuration["ConnectionStrings:default"];
                    Console.WriteLine($"Default Connection String - {connectionString}");
                })
                .ConfigureLogging((context, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddScoped<IAddCollectionHelper, AddCollectionHelper>();
                    services.AddScoped<IItemsCodeStatsHelper, ItemsCodeStatsHelper>();
                    services.AddScoped<IGeoJsonHelper, GeoJsonHelper>();
                    services.AddHostedService<ConsortiumCollectionsHostedService>();
                });
    }
}