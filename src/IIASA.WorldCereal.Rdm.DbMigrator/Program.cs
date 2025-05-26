using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace IIASA.WorldCereal.Rdm.DbMigrator;

class Program
{
    static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Volo.Abp", LogEventLevel.Warning)
#if DEBUG
            .MinimumLevel.Override("IIASA.WorldCereal.Rdm", LogEventLevel.Debug)
#else
                .MinimumLevel.Override("IIASA.WorldCereal.Rdm", LogEventLevel.Information)
#endif
            .Enrich.FromLogContext()
            .WriteTo.Async(c => c.File("Logs/logs.txt"))
            .WriteTo.Async(c => c.Console())
            .CreateLogger();

        var hostBuilder = new HostBuilder();
        hostBuilder.ConfigureAppConfiguration((hostingContext, config) =>
        {
            config.AddJsonFile("appsettings.json",
                optional: true,
                reloadOnChange: false).AddEnvironmentVariables();
        }).ConfigureServices((hostContext, services) => { services.AddHostedService<DbMigratorHostedService>(); });

        await hostBuilder.RunConsoleAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .AddAppSettingsSecretsJson()
            .ConfigureLogging((context, logging) => logging.ClearProviders())
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddJsonFile("appsettings.json",
                    optional: true,
                    reloadOnChange: false);
            })
            .ConfigureServices((hostContext, services) => { services.AddHostedService<DbMigratorHostedService>(); });
}