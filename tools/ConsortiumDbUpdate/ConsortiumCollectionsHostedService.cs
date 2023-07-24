using System.Threading;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Volo.Abp;

namespace ConsortiumDbUpdate
{
    public class ConsortiumCollectionsHostedService : IHostedService
    {
        private readonly IHostApplicationLifetime _hostApplicationLifetime;

        public ConsortiumCollectionsHostedService(IHostApplicationLifetime hostApplicationLifetime)
        {
            _hostApplicationLifetime = hostApplicationLifetime;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using (var application = AbpApplicationFactory.Create<RdmDbConsortiumModule>(options =>
            {
                options.UseAutofac();
                options.Services.AddLogging(c => c.AddSerilog());
                options.Services.AddAbpDbContext<RdmDbContext>();
            }))
            {
                application.Initialize();
                

                var rdmConsortiumService = application
                    .ServiceProvider
                    .GetRequiredService<RdmConsortiumService>();
                
                await rdmConsortiumService.Start();

                application.Shutdown();

                _hostApplicationLifetime.StopApplication();
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}