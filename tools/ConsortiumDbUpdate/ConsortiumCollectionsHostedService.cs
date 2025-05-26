using IIASA.WorldCereal.Rdm.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            
            using (var application = await AbpApplicationFactory.CreateAsync<RdmDbConsortiumModule>(options =>
                   {
                       options.UseAutofac();
                       //options.Services.AddLogging(c => c.AddSerilog());
                       options.Services.AddAbpDbContext<RdmDbContext>(x => x.AddDefaultRepositories(true));
                   }))
            {
                await application.InitializeAsync();
                

                var rdmConsortiumService = application
                    .ServiceProvider
                    .GetRequiredService<RdmConsortiumService>();
                
                await rdmConsortiumService.Start();

                await application.ShutdownAsync();

                _hostApplicationLifetime.StopApplication();
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}