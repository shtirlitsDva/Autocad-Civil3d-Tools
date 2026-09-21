using System.Text.Json;

using GDALService.Common;
using GDALService.Domain;

namespace GDALService.Protocol;

// Pieces of reply shape that more than one capability writes. Every number
// written comes from a type that only holds finite values, so there is no NaN
// to refuse.
internal static class WireFormat
{
    public static void Summary(Utf8JsonWriter json, SampleSummary sum)
    {
        json.WriteNumber("total", sum.Total);
        json.WriteNumber("ok", sum.OkCount);
        json.WriteNumber("outside", sum.OutsideCount);
        json.WriteNumber("noData", sum.NoDataCount);
        json.WriteNumber("err", sum.ErrCount);
    }

    // Only a sample with a height writes one; the others leave the field out.
    public static void Height(Utf8JsonWriter json, string name, Sample sample) =>
        sample.Height.Switch(metres => json.WriteNumber(name, metres), () => { });
}
