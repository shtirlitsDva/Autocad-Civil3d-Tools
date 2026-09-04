using EventManager;

using Xunit;

namespace GraphViewV3.Core.Tests;

/// <summary>
/// Covers the per-document unsubscribe bookkeeping the manager exposes as <c>Track</c> and drains
/// when a document is destroyed. Keyed on a stand-in, so this runs without AutoCAD.
/// </summary>
/// <remarks>
/// Nothing inside a tracked callback may use <c>Assert</c>: the ledger reports and swallows what a
/// callback throws, which would turn a failed assertion into a silent pass.
/// </remarks>
public class SubscriptionLedgerTests
{
    private sealed class Key
    {
        public Key(string name) => Name = name;
        public string Name { get; }
        public override string ToString() => Name;
    }

    private static SubscriptionLedger<Key> NewLedger(RecordingTrace trace) => new(trace.Trace);

    [Fact]
    public void TrackedCallbacksRunInOrderAndTheEntryIsDropped()
    {
        var trace = new RecordingTrace();
        var ledger = NewLedger(trace);
        var key = new Key("A");
        var order = new List<int>();
        ledger.Track(key, () => order.Add(1));
        ledger.Track(key, () => order.Add(2));

        Assert.True(ledger.Has(key));
        Assert.Equal(2, ledger.CountFor(key));

        ledger.Release(key);

        Assert.Equal(new[] { 1, 2 }, order);
        Assert.False(ledger.Has(key));
        Assert.Equal(0, ledger.CountFor(key));
        Assert.Empty(trace.Reports);
    }

    [Fact]
    public void AThrowingCallbackIsReportedAndTheRestStillRun()
    {
        // The leak this guards: one unsubscribe throwing against a half-torn-down database used
        // to abort the loop, leaving every later hook installed for the manager's life.
        var trace = new RecordingTrace();
        var ledger = NewLedger(trace);
        var key = new Key("A");
        var ran = new List<string>();

        ledger.Track(key, () => ran.Add("first"));
        ledger.Track(key, () => throw new InvalidOperationException("unsubscribe failed"));
        ledger.Track(key, () => ran.Add("third"));

        ledger.Release(key);

        Assert.Equal(new[] { "first", "third" }, ran);
        Assert.True(trace.Reported("tracked unsubscribe threw"));
        Assert.IsType<InvalidOperationException>(trace.Reports.Single().Error);
    }

    [Fact]
    public void AThrowingCallbackStillDropsTheEntry()
    {
        // The other half of the same leak: the key -- in production a dead Document -- must not
        // stay in the ledger pinning the object it names.
        var trace = new RecordingTrace();
        var ledger = NewLedger(trace);
        var key = new Key("A");
        ledger.Track(key, () => throw new InvalidOperationException("unsubscribe failed"));

        ledger.Release(key);

        Assert.False(ledger.Has(key));
        Assert.Empty(ledger.Counts());
    }

    [Fact]
    public void ACallbackThatTracksAgainstTheSameKeyDoesNotBreakTheIteration()
    {
        // Re-entrant Track used to throw "Collection was modified" out of the foreach, which is
        // the very failure the snapshot work was supposed to have removed.
        var trace = new RecordingTrace();
        var ledger = NewLedger(trace);
        var key = new Key("A");
        var ran = new List<string>();
        var lateRan = 0;

        ledger.Track(key, () =>
        {
            ran.Add("first");
            ledger.Track(key, () => lateRan++);
        });
        ledger.Track(key, () => ran.Add("second"));

        ledger.Release(key);

        Assert.Equal(new[] { "first", "second" }, ran);
        Assert.Empty(trace.Reports);

        // The late registration is discarded with the rest: the key is going away.
        Assert.Equal(0, lateRan);
        Assert.False(ledger.Has(key));
    }

    [Fact]
    public void ReleasingAnUnknownKeyIsANoOp()
    {
        var trace = new RecordingTrace();
        var ledger = NewLedger(trace);

        ledger.Release(new Key("nobody"));

        Assert.Empty(trace.Reports);
    }

    [Fact]
    public void OneKeyIsReleasedWithoutTouchingTheOthers()
    {
        var trace = new RecordingTrace();
        var ledger = NewLedger(trace);
        var a = new Key("A");
        var b = new Key("B");
        var ran = new List<string>();
        ledger.Track(a, () => ran.Add("a"));
        ledger.Track(b, () => ran.Add("b"));

        ledger.Release(a);

        Assert.Equal(new[] { "a" }, ran);
        Assert.False(ledger.Has(a));
        Assert.True(ledger.Has(b));
    }

    [Fact]
    public void CountsReportsTheTrackedTotalPerKey()
    {
        var trace = new RecordingTrace();
        var ledger = NewLedger(trace);
        var a = new Key("A");
        var b = new Key("B");
        ledger.Track(a, () => { });
        ledger.Track(a, () => { });
        ledger.Track(b, () => { });

        var counts = ledger.Counts();

        Assert.Equal(2, counts[a]);
        Assert.Equal(1, counts[b]);
    }

    [Fact]
    public void ReleaseAllRunsEveryKeyAndEmptiesTheLedger()
    {
        var trace = new RecordingTrace();
        var ledger = NewLedger(trace);
        var ran = new List<string>();
        ledger.Track(new Key("A"), () => ran.Add("a"));
        ledger.Track(new Key("B"), () => ran.Add("b"));

        ledger.ReleaseAll();

        Assert.Equal(2, ran.Count);
        Assert.Empty(ledger.Counts());
    }

    [Fact]
    public void ReleaseAllSurvivesAThrowingCallbackInEveryEntry()
    {
        // Dispose calls this first. A throw escaping here would abort the whole teardown, and a
        // retried Dispose returns early -- every hook from the previous load would stay installed.
        var trace = new RecordingTrace();
        var ledger = NewLedger(trace);
        var ran = 0;
        ledger.Track(new Key("A"), () => throw new InvalidOperationException("a failed"));
        ledger.Track(new Key("B"), () => throw new InvalidOperationException("b failed"));
        ledger.Track(new Key("C"), () => ran++);

        ledger.ReleaseAll();

        Assert.Equal(1, ran);
        Assert.Equal(2, trace.Reports.Count);
        Assert.Empty(ledger.Counts());
    }

    [Fact]
    public void ReleaseAllLeavesNothingBehindWhenACallbackReTracks()
    {
        var trace = new RecordingTrace();
        var ledger = NewLedger(trace);
        var a = new Key("A");
        ledger.Track(a, () => ledger.Track(a, () => { }));

        ledger.ReleaseAll();

        Assert.Empty(ledger.Counts());
        Assert.False(ledger.Has(a));
    }

    [Fact]
    public void TrackRejectsANullCallback()
    {
        var ledger = NewLedger(new RecordingTrace());

        Assert.Throws<ArgumentNullException>(() => ledger.Track(new Key("A"), null!));
    }
}
