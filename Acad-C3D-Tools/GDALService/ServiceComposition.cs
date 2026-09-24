using GDALService.Capabilities;
using GDALService.Common;
using GDALService.Hosting;
using GDALService.Project;
using GDALService.Protocol;
using GDALService.Terrain;
using GDALService.Terrain.GdalBackend;

using Microsoft.Extensions.DependencyInjection;

namespace GDALService;

// The service's object graph, in one place. The executable and the tests build
// it the same way; tests pass `configure` to swap an edge for a fake or to add
// a capability. This and Program are the only code that names an edge
// implementation.
internal static class ServiceComposition
{
    public static ServiceProvider Build(ServiceStreams streams, Result<string> gdal, SamplingOptions options,
                                        Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection()
            .AddSingleton(streams)
            .AddSingleton(new GdalStatus(gdal))
            .AddSingleton(options)
            .AddSingleton(new ServiceLog(streams.Error))
            .AddSingleton<ProgressFactory>()
            .AddSingleton<Sampler>()
            .AddSingleton<ITileCatalog, FileSystemTileCatalog>()
            .AddSingleton<IRasterFactory>(provider => gdal switch
            {
                Ok<string> => new GdalRasterFactory(provider.GetRequiredService<ServiceLog>()),
                Fault why => new UnavailableRasterFactory(why),
            })
            .AddSingleton<ProjectStore>()
            .AddCapability<Hello>()
            .AddCapability<SetProject>()
            .AddCapability<SamplePoints>()
            .AddCapability<SampleGrid>()
            .AddCapability<Shutdown>();
        configure(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    // The loop over every registered capability - or the reason there is none:
    // two capabilities claiming one type is a wiring bug found at start-up.
    public static Result<ServiceLoop> Loop(IServiceProvider provider) =>
        CapabilityRegistry.Create(provider.GetServices<ICapability>()).Map(registry =>
            ActivatorUtilities.CreateInstance<ServiceLoop>(provider, registry));
}
