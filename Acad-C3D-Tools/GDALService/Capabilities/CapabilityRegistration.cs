using Microsoft.Extensions.DependencyInjection;

namespace GDALService.Capabilities;

internal static class CapabilityRegistration
{
    // The one line a new capability adds to ServiceComposition. The container
    // builds it with whatever its constructor asks for.
    public static IServiceCollection AddCapability<T>(this IServiceCollection services) where T : class, ICapability =>
        services.AddSingleton<ICapability, T>();
}
