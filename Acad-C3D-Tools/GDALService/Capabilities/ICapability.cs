using GDALService.Common;
using GDALService.Protocol;

namespace GDALService.Capabilities;

// One request type the service answers. A capability reads its own payload,
// does its own work and shapes its own reply, so adding one touches nothing
// else: write the class, register it with AddCapability in ServiceComposition.
internal interface ICapability
{
    // The wire's "type", matched exactly, e.g. "SAMPLE_POINTS".
    string Type { get; }

    Result<Reply> Handle(Envelope request);
}
