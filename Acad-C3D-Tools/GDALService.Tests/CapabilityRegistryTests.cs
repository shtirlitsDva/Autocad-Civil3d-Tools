using GDALService.Capabilities;
using GDALService.Common;
using GDALService.Hosting;
using GDALService.Protocol;
using GDALService.Terrain;

using Microsoft.Extensions.DependencyInjection;

namespace GDALService.Tests;

public class CapabilityRegistryTests
{
    private sealed class Named(string type) : ICapability
    {
        public string Type => type;

        public Result<Reply> Handle(Envelope request) => new Fault(FaultKind.Internal, "not called");
    }

    [Fact]
    public void A_type_claimed_twice_is_refused_by_name()
    {
        var fault = Expect.Fault(CapabilityRegistry.Create([new Named("A"), new Named("B"), new Named("A")]));
        Assert.Equal(FaultKind.Internal, fault.Kind);
        Assert.Equal("capability type registered more than once: A", fault.Message);
    }

    [Fact]
    public void An_unknown_type_is_invalid_and_types_match_exactly()
    {
        var registry = Expect.Ok(CapabilityRegistry.Create([new Named("HELLO")]));

        Assert.Equal("Unknown type 'hello'", Expect.Fault(registry.Find("hello")).Message);
        Assert.Equal("HELLO", Expect.Ok(registry.Find("HELLO")).Type);
    }

    // Spec decision D7: the registration line cannot be forgotten. Every
    // capability class in the service is registered by the real composition,
    // and that composition builds under ValidateOnBuild.
    [Fact]
    public void Every_capability_in_the_service_is_registered_by_the_composition()
    {
        var streams = new ServiceStreams(TextReader.Null, TextWriter.Null, TextWriter.Null);
        using var provider = ServiceComposition.Build(streams, GdalForTests.Loaded, new SamplingOptions(), _ => { });

        var registered = provider.GetServices<ICapability>().Select(c => c.GetType()).ToHashSet();
        var declared = typeof(ICapability).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ICapability).IsAssignableFrom(t))
            .ToHashSet();

        Assert.NotEmpty(declared);
        Assert.Equal(declared, registered);
        Assert.IsType<ServiceLoop>(Expect.Ok(ServiceComposition.Loop(provider)));
    }

    [Fact]
    public void A_duplicate_registration_stops_the_service_before_it_serves()
    {
        var streams = new ServiceStreams(TextReader.Null, TextWriter.Null, TextWriter.Null);
        using var provider = ServiceComposition.Build(streams, GdalForTests.Loaded, new SamplingOptions(),
                                                      services => services.AddCapability<Hello>());

        Assert.Equal("capability type registered more than once: HELLO", Expect.Fault(ServiceComposition.Loop(provider)).Message);
    }
}
