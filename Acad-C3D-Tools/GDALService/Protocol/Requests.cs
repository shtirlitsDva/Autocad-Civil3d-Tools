using GDALService.Domain;

namespace GDALService.Protocol;

internal sealed record RequestId(string Value);

// A line whose id could not be read. Its reply carries no id at all rather than
// an invented one; the client then reports the reply as unmatched.
internal sealed record Unaddressed
{
    public static readonly Unaddressed Instance = new();
    private Unaddressed() { }
}

internal union ReplyTo(RequestId, Unaddressed);

internal sealed record Hello(RequestId Id);
internal sealed record SetProject(RequestId Id, string ProjectId, string BasePath);
internal sealed record SamplePoints(RequestId Id, IReadOnlyList<PointQuery> Points);
internal sealed record SampleGrid(RequestId Id, double GridDist);
internal sealed record Shutdown(RequestId Id);

// Every request the service understands. A new request type is a new case here,
// and every switch over Request stops compiling until it handles it.
internal union Request(Hello, SetProject, SamplePoints, SampleGrid, Shutdown);
