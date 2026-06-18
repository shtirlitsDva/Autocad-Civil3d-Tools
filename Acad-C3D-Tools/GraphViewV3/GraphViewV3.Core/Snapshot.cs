namespace GraphViewV3.Core;

/// <summary>One pipe (an FJV polyline). Endpoint ports are P0/P1; Vertices carries the
/// full plan geometry so welded studs can connect mid-span (not only at a vertex).</summary>
public sealed record PipeDto(
    string Handle,
    string Layer,
    string System,
    string Size,
    Pt P0,
    Pt P1,
    double Length,
    IReadOnlyList<Pt> Vertices);

/// <summary>One FJV component (a dynamic block). Ports are the WCS positions of its nested
/// MuffeIntern* coupling blocks (resolved on the AutoCAD side). Weldable studs
/// (afgrstuds / sh-lige / sh-vinkel) may connect via their <see cref="WeldPort"/> landing
/// anywhere along a pipe, not just at an endpoint.</summary>
public sealed record ComponentDto(
    string Handle,
    string Name,
    Pt Position,
    IReadOnlyList<Pt> Ports,
    bool Weldable = false,
    Pt? WeldPort = null);

/// <summary>An immutable snapshot of the FJV network read from the drawing. Carries a
/// content hash so the live loop can skip rebuilds when nothing relevant changed.</summary>
public sealed class NetworkSnapshot
{
    public IReadOnlyList<PipeDto> Pipes { get; }
    public IReadOnlyList<ComponentDto> Components { get; }
    public long ContentHash { get; }

    public NetworkSnapshot(IReadOnlyList<PipeDto> pipes, IReadOnlyList<ComponentDto> components)
    {
        Pipes = pipes;
        Components = components;
        ContentHash = ComputeHash(pipes, components);
    }

    public static readonly NetworkSnapshot Empty =
        new(Array.Empty<PipeDto>(), Array.Empty<ComponentDto>());

    // FNV-1a over handles + quantized geometry. Quantizing to mm avoids hash churn
    // from sub-mm float noise while still catching real moves.
    private static long ComputeHash(IReadOnlyList<PipeDto> pipes, IReadOnlyList<ComponentDto> components)
    {
        unchecked
        {
            const long prime = 1099511628211;
            long h = unchecked((long)14695981039346656037);
            void Mix(string s) { foreach (char c in s) { h ^= c; h *= prime; } }
            void MixD(double d) { h ^= (long)Math.Round(d * 1000.0); h *= prime; }

            foreach (var p in pipes)
            {
                Mix(p.Handle); MixD(p.P0.X); MixD(p.P0.Y); MixD(p.P1.X); MixD(p.P1.Y);
                foreach (var v in p.Vertices) { MixD(v.X); MixD(v.Y); }
            }
            foreach (var c in components)
            {
                Mix(c.Handle); MixD(c.Position.X); MixD(c.Position.Y);
                foreach (var port in c.Ports) { MixD(port.X); MixD(port.Y); }
            }
            return h;
        }
    }
}
