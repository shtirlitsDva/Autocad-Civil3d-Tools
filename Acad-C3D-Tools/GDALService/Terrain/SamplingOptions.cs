namespace GDALService.Terrain;

// The service's sampling limits, in one place instead of literals in the
// sampler. The defaults are what the service has always used.
internal sealed record SamplingOptions(int MaxGridPoints = 5_000_000, int Workers = 0, int ProgressEvery = 500)
{
    // Workers = 0 leaves one core to the rest of the machine.
    public int EffectiveWorkers => Workers > 0 ? Workers : Math.Max(1, Environment.ProcessorCount - 1);
}
