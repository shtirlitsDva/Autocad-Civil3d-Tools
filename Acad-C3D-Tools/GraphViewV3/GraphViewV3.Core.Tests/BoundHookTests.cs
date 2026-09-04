using EventManager;

using Xunit;

namespace GraphViewV3.Core.Tests;

/// <summary>
/// Covers the binding that follows the active document's database. Attach and detach are injected
/// as actions over a stand-in target, so this runs without AutoCAD.
/// </summary>
public class BoundHookTests
{
    private sealed class Target
    {
        public Target(string name) => Name = name;
        public string Name { get; }
        public override string ToString() => Name;
    }

    private sealed class Probe
    {
        public readonly RecordingTrace Trace = new();
        public readonly List<string> Log = new();

        /// <summary>How many of the three handlers are currently attached, per target.</summary>
        public readonly Dictionary<Target, int> Attached = new();

        /// <summary>Attach throws once this many handlers are on -- 0 = the very first one.</summary>
        public int AttachThrowsAfter = int.MaxValue;

        public bool DetachThrows;

        public BoundHook<Target> New() => new(Attach, Detach, Trace.Trace);

        public int CountOn(Target t) => Attached.TryGetValue(t, out var n) ? n : 0;

        private void Attach(Target t)
        {
            Log.Add($"attach {t}");
            while (CountOn(t) < 3)
            {
                if (CountOn(t) >= AttachThrowsAfter)
                    throw new InvalidOperationException($"attach to {t} failed");
                Attached[t] = CountOn(t) + 1;
            }
        }

        private void Detach(Target t)
        {
            Log.Add($"detach {t}");
            if (DetachThrows) throw new InvalidOperationException($"detach from {t} failed");
            Attached[t] = 0;
        }
    }

    [Fact]
    public void BindingAttachesAndClaimsTheTarget()
    {
        var probe = new Probe();
        var binding = probe.New();
        var db = new Target("A");

        binding.BindTo(db);

        Assert.Same(db, binding.Bound);
        Assert.Equal(3, probe.CountOn(db));
        Assert.Equal(new[] { "attach A" }, probe.Log);
    }

    [Fact]
    public void BindingToTheSameTargetAgainDoesNothing()
    {
        var probe = new Probe();
        var binding = probe.New();
        var db = new Target("A");

        binding.BindTo(db);
        binding.BindTo(db);

        Assert.Equal(new[] { "attach A" }, probe.Log);
    }

    [Fact]
    public void RebindingDetachesThePreviousTargetBeforeAttachingTheNewOne()
    {
        var probe = new Probe();
        var binding = probe.New();
        var a = new Target("A");
        var b = new Target("B");

        binding.BindTo(a);
        binding.BindTo(b);

        Assert.Equal(new[] { "attach A", "detach A", "attach B" }, probe.Log);
        Assert.Same(b, binding.Bound);
        Assert.Equal(0, probe.CountOn(a));
        Assert.Equal(3, probe.CountOn(b));
    }

    [Fact]
    public void BindingToNullDetachesAndLetsGo()
    {
        var probe = new Probe();
        var binding = probe.New();
        var a = new Target("A");
        binding.BindTo(a);

        binding.BindTo(null);

        Assert.Null(binding.Bound);
        Assert.Equal(0, probe.CountOn(a));
    }

    [Fact]
    public void BindingToNullWhenNothingIsBoundDoesNothing()
    {
        var probe = new Probe();
        var binding = probe.New();

        binding.BindTo(null);

        Assert.Empty(probe.Log);
        Assert.Null(binding.Bound);
    }

    [Fact]
    public void AFailingDetachIsReportedAndTheNewTargetStillBinds()
    {
        // The #67 case: AutoCAD has already torn the previous database down, so unsubscribing
        // from it throws. That must neither escape into AutoCAD's dispatch nor stop the rebind.
        var probe = new Probe { DetachThrows = true };
        var binding = probe.New();
        var a = new Target("A");
        var b = new Target("B");
        binding.BindTo(a);

        binding.BindTo(b);

        Assert.Same(b, binding.Bound);
        Assert.Equal(3, probe.CountOn(b));
        Assert.True(probe.Trace.Reported("detach from the previously bound target"));
    }

    [Fact]
    public void AFailingDetachDoesNotLeaveTheDeadTargetClaimed()
    {
        var probe = new Probe { DetachThrows = true };
        var binding = probe.New();
        var a = new Target("A");
        binding.BindTo(a);

        binding.BindTo(null);

        Assert.Null(binding.Bound);
        Assert.True(probe.Trace.Reported("detach from the previously bound target"));
    }

    [Fact]
    public void APartialAttachIsUnwoundAndLeavesNothingClaimed()
    {
        // Two of three handlers go on and the third throws. Claiming the target here is what made
        // the identity short-circuit turn every later rebind into a no-op.
        var probe = new Probe { AttachThrowsAfter = 2 };
        var binding = probe.New();
        var a = new Target("A");

        binding.BindTo(a);

        Assert.Null(binding.Bound);
        Assert.Equal(0, probe.CountOn(a));
        Assert.Equal(new[] { "attach A", "detach A" }, probe.Log);
        Assert.True(probe.Trace.Reported("attach to the new target"));
    }

    [Fact]
    public void AfterAFailedAttachALaterBindToTheSameTargetRetriesForReal()
    {
        var probe = new Probe { AttachThrowsAfter = 0 };
        var binding = probe.New();
        var a = new Target("A");

        binding.BindTo(a);
        Assert.Null(binding.Bound);

        probe.AttachThrowsAfter = int.MaxValue;
        binding.BindTo(a);

        Assert.Same(a, binding.Bound);
        Assert.Equal(3, probe.CountOn(a));
    }

    [Fact]
    public void AFailingAttachDoesNotEscape_AndAFailingUnwindDoesNotEither()
    {
        var probe = new Probe { AttachThrowsAfter = 1, DetachThrows = true };
        var binding = probe.New();
        var a = new Target("A");

        binding.BindTo(a);

        Assert.Null(binding.Bound);
        Assert.True(probe.Trace.Reported("attach to the new target"));
        Assert.True(probe.Trace.Reported("unwind a partial attach"));
    }
}
