using System.Text.Json;

using GDALService.Common;
using GDALService.Domain;
using GDALService.Protocol;

namespace GDALService.Tests;

public class ReplyWriterTests
{
    private static JsonElement Write(ReplyTo to, Result<ReplyBody> reply) =>
        JsonDocument.Parse(ReplyWriter.Serialize(to, reply)).RootElement.Clone();

    private static readonly RequestId Id = new("7");

    private static SampledPoint Row(int seq, Sample sample) => new(new PointQuery(1, seq, 0, 10 + seq, 20), sample);

    [Fact]
    public void Only_an_elevation_row_carries_elev()
    {
        Sample[] samples = [new Elevation(12.5), NoData.Instance, Outside.Instance, new ReadFailed("disk")];
        var rows = samples.Select((s, i) => Row(i, s)).ToList();

        var json = Write(Id, new Ok<ReplyBody>(new PointsSampled(rows, SampleSummary.Of(samples))));

        Assert.Equal(0, json.GetProperty("status").GetInt32());
        var result = json.GetProperty("result");
        Assert.Equal((4, 1, 1, 1, 1), (result.GetProperty("total").GetInt32(), result.GetProperty("ok").GetInt32(),
            result.GetProperty("outside").GetInt32(), result.GetProperty("noData").GetInt32(), result.GetProperty("err").GetInt32()));

        var written = result.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(["OK", "NODATA", "OUTSIDE", "ERR"], written.Select(r => r.GetProperty("status").GetString()));
        Assert.Equal(12.5, written[0].GetProperty("elev").GetDouble());
        Assert.All(written.Skip(1), r => Assert.False(r.TryGetProperty("elev", out _)));
        Assert.All(written, r => Assert.Equal(1, r.GetProperty("geomId").GetInt64()));
    }

    [Fact]
    public void A_failure_has_an_error_and_no_result()
    {
        var json = Write(Id, new Fault(FaultKind.NotInitialized, "no project"));

        Assert.Equal("7", json.GetProperty("id").GetString());
        Assert.Equal(4, json.GetProperty("status").GetInt32());
        Assert.Equal("no project", json.GetProperty("error").GetString());
        Assert.False(json.TryGetProperty("result", out _));
    }

    [Fact]
    public void A_success_has_a_result_and_no_error()
    {
        var json = Write(Id, new Ok<ReplyBody>(HelloAck.Instance));

        Assert.Equal("HELLO_ACK", json.GetProperty("result").GetProperty("msg").GetString());
        Assert.False(json.TryGetProperty("error", out _));
    }

    [Fact]
    public void An_unaddressed_reply_has_no_id()
    {
        var json = Write(Unaddressed.Instance, new Fault(FaultKind.InvalidArgs, "Bad JSON"));
        Assert.False(json.TryGetProperty("id", out _));
    }

    [Theory]
    [InlineData(nameof(FaultKind.Gdal), 1)]
    [InlineData(nameof(FaultKind.Internal), 1)]
    [InlineData(nameof(FaultKind.InvalidArgs), 2)]
    [InlineData(nameof(FaultKind.NotFound), 3)]
    [InlineData(nameof(FaultKind.NotInitialized), 4)]
    public void Fault_kinds_keep_the_old_wire_status_codes(string kind, int status) =>
        Assert.Equal(status, ReplyWriter.StatusOf(Enum.Parse<FaultKind>(kind)));

    [Fact]
    public void A_grid_lists_only_cells_on_the_raster_and_only_elevations_carry_z()
    {
        GridSample[] cells =
        [
            new(0, 0, new Elevation(5)), new(1, 0, NoData.Instance),
            new(2, 0, Outside.Instance), new(3, 0, new ReadFailed("x")),
        ];

        var json = Write(Id, new Ok<ReplyBody>(new GridSampled(cells, SampleSummary.Of(cells.Select(c => c.Sample)))));

        var rows = json.GetProperty("result").GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(["OK", "NODATA"], rows.Select(r => r.GetProperty("status").GetString()));
        Assert.Equal(5, rows[0].GetProperty("z").GetDouble());
        Assert.False(rows[1].TryGetProperty("z", out _));
        Assert.Equal(4, json.GetProperty("result").GetProperty("total").GetInt32());
    }

    [Fact]
    public void A_project_without_a_projection_omits_it()
    {
        var json = Write(Id, new Ok<ReplyBody>(new ProjectOpened("P", @"C:\p\Elevations", "/vsimem/x.vrt", 2, 3, 1, None.Instance)));
        Assert.False(json.GetProperty("result").TryGetProperty("projection", out _));
        Assert.Equal(3, json.GetProperty("result").GetProperty("height").GetInt32());
    }
}
