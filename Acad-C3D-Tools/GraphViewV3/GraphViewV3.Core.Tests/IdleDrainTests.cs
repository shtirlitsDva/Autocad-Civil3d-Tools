using EventManager;

using Xunit;

namespace GraphViewV3.Core.Tests;

/// <summary>
/// Covers the shared arm-work-disarm idle debounce behind <c>AcadEventManager.RunOnNextIdle</c>.
/// The idle hook is injected as an arm/disarm pair, so this runs without AutoCAD.
/// </summary>
/// <remarks>
/// Nothing in a queued action may use <c>Assert</c>: the drain reports and swallows what an action
/// throws, which would turn a failed assertion into a silent pass. Every test therefore records
/// what it observed into a local and asserts on that after the drain has returned.
/// </remarks>
public class IdleDrainTests
{
    private sealed class IdleHook
    {
        public int Arms;
        public int Disarms;
        public bool Live;
        public bool ArmThrows;
        public bool DisarmThrows;
        public readonly RecordingTrace Trace = new();

        public IdleDrain NewDrain() => new(Arm, Disarm, Trace.Trace);

        private void Arm()
        {
            if (ArmThrows) throw new InvalidOperationException("arm failed");
            Arms++;
            Live = true;
        }

        private void Disarm()
        {
            Disarms++;
            Live = false;
            if (DisarmThrows) throw new InvalidOperationException("disarm failed");
        }
    }

    [Fact]
    public void TheFirstEnqueueArms_FurtherOnesDoNot()
    {
        var hook = new IdleHook();
        var drain = hook.NewDrain();

        drain.Enqueue(() => { });
        drain.Enqueue(() => { });

        Assert.Equal(1, hook.Arms);
        Assert.True(hook.Live);
        Assert.True(drain.Armed);
        Assert.Equal(2, drain.Pending);
    }

    [Fact]
    public void OneTickRunsEverythingQueuedInOrderAndThenReleasesTheHook()
    {
        var hook = new IdleHook();
        var drain = hook.NewDrain();
        var order = new List<int>();
        drain.Enqueue(() => order.Add(1));
        drain.Enqueue(() => order.Add(2));

        drain.Tick();

        Assert.Equal(new[] { 1, 2 }, order);
        Assert.Equal(0, drain.Pending);
        Assert.False(drain.Armed);
        Assert.False(hook.Live);
        Assert.Equal(1, hook.Disarms);
        Assert.Empty(hook.Trace.Reports);
    }

    [Fact]
    public void ATickThatFindsNothingQueuedStillReleasesTheHookAndRunsNothing()
    {
        var hook = new IdleHook();
        var drain = hook.NewDrain();
        var ran = 0;
        drain.Enqueue(() => ran++);

        drain.Tick();
        drain.Tick();

        Assert.Equal(1, ran);
        Assert.False(hook.Live);
        Assert.False(drain.Armed);
    }

    [Fact]
    public void EachActionRunsExactlyOnceAndIsThenForgotten()
    {
        var hook = new IdleHook();
        var drain = hook.NewDrain();
        var ran = 0;
        drain.Enqueue(() => ran++);

        drain.Tick();
        drain.Tick();
        drain.Tick();

        Assert.Equal(1, ran);
    }

    [Fact]
    public void WorkQueuedFromInsideTheDrainRunsOnTheNextTick_NotThisOne()
    {
        var hook = new IdleHook();
        var drain = hook.NewDrain();
        var seen = new List<string>();

        drain.Enqueue(() =>
        {
            seen.Add("first");
            drain.Enqueue(() => seen.Add("second"));
        });

        drain.Tick();
        Assert.Equal(new[] { "first" }, seen);

        // Re-armed from inside the drain, so a later tick picks the new generation up.
        Assert.True(drain.Armed);
        Assert.Equal(2, hook.Arms);

        drain.Tick();
        Assert.Equal(new[] { "first", "second" }, seen);
    }

    [Fact]
    public void ANestedTickDuringADrainBailsOutWithoutConsumingTheArming()
    {
        // The modal-dialog case the invalidation pump depends on: a queued action pumps messages,
        // AutoCAD re-raises Idle with the drain still on the stack, and whatever the modal armed
        // must survive that nested tick.
        var hook = new IdleHook();
        var drain = hook.NewDrain();
        var laterRan = 0;
        var armedAfterNestedTick = false;
        var disarmsDuringNestedTick = 0;

        drain.Enqueue(() =>
        {
            drain.Enqueue(() => laterRan++);
            var before = hook.Disarms;

            drain.Tick();

            disarmsDuringNestedTick = hook.Disarms - before;
            armedAfterNestedTick = drain.Armed;
        });

        drain.Tick();

        Assert.Equal(0, disarmsDuringNestedTick);
        Assert.True(armedAfterNestedTick);
        Assert.Equal(0, laterRan);
        Assert.Empty(hook.Trace.Reports);

        drain.Tick();
        Assert.Equal(1, laterRan);
    }

    [Fact]
    public void AThrowingActionIsReportedAndTheOnesQueuedBehindItStillRun()
    {
        var hook = new IdleHook();
        var drain = hook.NewDrain();
        var seen = new List<string>();

        drain.Enqueue(() => seen.Add("before"));
        drain.Enqueue(() => throw new InvalidOperationException("action failed"));
        drain.Enqueue(() => seen.Add("after"));

        drain.Tick();

        Assert.Equal(new[] { "before", "after" }, seen);
        Assert.True(hook.Trace.Reported("queued for the next idle threw"));
        Assert.IsType<InvalidOperationException>(hook.Trace.Reports.Single().Error);
        Assert.False(drain.Draining);
    }

    [Fact]
    public void AFailingArmTakesTheActionBackOffTheQueueAndSurfacesTheException()
    {
        var hook = new IdleHook { ArmThrows = true };
        var drain = hook.NewDrain();

        Assert.Throws<InvalidOperationException>(() => drain.Enqueue(() => { }));

        Assert.False(drain.Armed);
        Assert.Equal(0, drain.Pending);
    }

    [Fact]
    public void AfterAFailingArmALaterEnqueueStillArms()
    {
        var hook = new IdleHook { ArmThrows = true };
        var drain = hook.NewDrain();
        Assert.Throws<InvalidOperationException>(() => drain.Enqueue(() => { }));

        hook.ArmThrows = false;
        var ran = 0;
        drain.Enqueue(() => ran++);

        Assert.True(drain.Armed);
        Assert.Equal(1, hook.Arms);

        drain.Tick();
        Assert.Equal(1, ran);
    }

    [Fact]
    public void AFailingDisarmIsReportedAndTheQueuedWorkStillRuns()
    {
        var hook = new IdleHook { DisarmThrows = true };
        var drain = hook.NewDrain();
        var ran = 0;
        drain.Enqueue(() => ran++);

        drain.Tick();

        Assert.Equal(1, ran);
        Assert.False(drain.Armed);
        Assert.True(hook.Trace.Reported("release the idle hook"));
    }

    [Fact]
    public void AbandonDropsTheQueueAndReleasesTheHook()
    {
        var hook = new IdleHook();
        var drain = hook.NewDrain();
        var ran = 0;
        drain.Enqueue(() => ran++);

        drain.Abandon();

        Assert.Equal(0, drain.Pending);
        Assert.False(drain.Armed);
        Assert.False(hook.Live);
        Assert.Equal(1, hook.Disarms);

        drain.Tick();
        Assert.Equal(0, ran);
    }

    [Fact]
    public void AbandonWhenNothingIsArmedDoesNotTouchTheHook()
    {
        var hook = new IdleHook();
        var drain = hook.NewDrain();

        drain.Abandon();

        Assert.Equal(0, hook.Disarms);
    }

    [Fact]
    public void AbandonCannotWithdrawTheGenerationAlreadyInFlight()
    {
        // What Dispose promises, precisely: nothing further is queued and no tick is armed. The
        // generation the drain is holding in its local array runs to the end regardless.
        var hook = new IdleHook();
        var drain = hook.NewDrain();
        var seen = new List<string>();

        drain.Enqueue(() =>
        {
            seen.Add("first");
            drain.Enqueue(() => seen.Add("queued during the drain"));
            drain.Abandon();
        });
        drain.Enqueue(() => seen.Add("second"));

        drain.Tick();

        Assert.Equal(new[] { "first", "second" }, seen);
        Assert.Equal(0, drain.Pending);
        Assert.False(drain.Armed);
        Assert.False(drain.Draining);
    }

    [Fact]
    public void EnqueueRejectsNull()
    {
        var drain = new IdleHook().NewDrain();

        Assert.Throws<ArgumentNullException>(() => drain.Enqueue(null!));
    }
}
