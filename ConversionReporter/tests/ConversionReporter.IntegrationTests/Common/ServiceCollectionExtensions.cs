using ConversionReporter.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConversionReporter.IntegrationTests.Common;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplication()
        {
            return DependencyInjection.AddApplication(services);
        }

        public IServiceCollection AddPersistence(string connectionString)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] = connectionString
                    })
                .Build();

            return Infrastructure.Persistence.DependencyInjection
                .AddPersistence(services, config);
        }

        public IServiceCollection AddCaching(string connectionString)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Redis"] = connectionString
                    })
                .Build();

            return Infrastructure.Caching.DependencyInjection
                .AddCaching(services, config);
        }
    }
}