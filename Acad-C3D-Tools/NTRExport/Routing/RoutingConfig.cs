namespace NTRExport.Routing
{
    internal sealed class RoutingConfig
    {
        public double PreinsulatedLegMeters { get; init; } = 1.0;
        public double MinStraightMeters { get; init; } = 0.25;
        public double DefaultBendRadiusMeters { get; init; } = 0.3;
        public double TeeOffsetMeters { get; init; } = 0.4;
    }
}

