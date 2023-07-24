using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using IIASA.WorldCereal.Rdm.Data;
using Volo.Abp.DependencyInjection;

namespace IIASA.WorldCereal.Rdm.EntityFrameworkCore
{
    public class EntityFrameworkCoreRdmDbSchemaMigrator
        : IRdmDbSchemaMigrator, ITransientDependency
    {
        private readonly IServiceProvider _serviceProvider;

        public EntityFrameworkCoreRdmDbSchemaMigrator(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task MigrateAsync()
        {
            /* We intentionally resolving the RdmMigrationsDbContext
             * from IServiceProvider (instead of directly injecting it)
             * to properly get the connection string of the current tenant in the
             * current scope.
             */

            await _serviceProvider
                .GetRequiredService<RdmMigrationsDbContext>()
                .Database
                .MigrateAsync();
        }
    }
}