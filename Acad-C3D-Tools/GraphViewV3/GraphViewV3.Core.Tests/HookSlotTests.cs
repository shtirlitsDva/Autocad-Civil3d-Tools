using EventManager;

using Xunit;

namespace GraphViewV3.Core.Tests;

/// <summary>
/// Covers the dispatch and install bookkeeping of the shared EventManager's hook slot. The slot is
/// linked in as source (it is plain BCL code), so this runs without AutoCAD.
/// </summary>
public class HookSlotTests
{
    private sealed class Hook
    {
        public int Installs;
        public int Uninstalls;
        public bool InstallThrows;
        public bool UninstallThrows;
        public bool Live;
        public readonly RecordingTrace Trace = new();

        public HookSlot<Action> NewSlot() => new(Install, Uninstall, Trace.Trace);

        private void Install()
        {
            if (InstallThrows) throw new InvalidOperationException("install failed");
            Installs++;
            Live = true;
        }

        private void Uninstall()
        {
            Uninstalls++;
            Live = false;
            if (UninstallThrows) throw new InvalidOperationException("uninstall failed");
        }
    }

    [Fact]
    public void FirstHandlerInstallsTheHook_FurtherHandlersDoNot()
    {
        var hook = new Hook();
        var slot = hook.NewSlot();

        slot.Add(() => { });
        slot.Add(() => { });

        Assert.Equal(1, hook.Installs);
        Assert.True(hook.Live);
        Assert.True(slot.Installed);
    }

    [Fact]
    public void NoHandlers_MeansNoHookAndNothingToInvoke()
    {
        var hook = new Hook();
        var slot = hook.NewSlot();

        Assert.False(slot.Installed);
        Assert.Equal(0, hook.Installs);
        Assert.Null(slot.Handlers);
    }

    [Fact]
    public void HandlersDispatchInSubscriptionOrder()
    {
        var slot = new Hook().NewSlot();
        var order = new List<int>();
        slot.Add(() => order.Add(1));
        slot.Add(() => order.Add(2));

        slot.Handlers?.Invoke();

        Assert.Equal(new[] { 1, 2 }, order);
    }

    [Fact]
    public void HandlerUnsubscribingItselfDuringDispatch_DoesNotThrowAndLetsTheRestRun()
    {
        var slot = new Hook().NewSlot();
        var seen = new List<string>();

        Action? first = null;
        first = () =>
        {
            seen.Add("first");
            slot.Remove(first);
        };
        slot.Add(first);
        slot.Add(() => seen.Add("second"));

        slot.Handlers?.Invoke();

        Assert.Equal(new[] { "first", "second" }, seen);

        // The removal took effect for the next dispatch, not this one.
        seen.Clear();
        slot.Handlers?.Invoke();
        Assert.Equal(new[] { "second" }, seen);
    }

    [Fact]
    public void HandlerSubscribingAnotherDuringDispatch_DoesNotThrowAndDefersToTheNextDispatch()
    {
        var slot = new Hook().NewSlot();
        var seen = new List<string>();
        Action late = () => seen.Add("late");

        slot.Add(() =>
        {
            seen.Add("early");
            slot.Add(late);
        });

        slot.Handlers?.Invoke();
        Assert.Equal(new[] { "early" }, seen);

        seen.Clear();
        slot.Handlers?.Invoke();
        Assert.Equal(new[] { "early", "late" }, seen);
    }

    [Fact]
    public void HandlerRemovingAnotherDuringDispatch_StillInvokesItThisRound()
    {
        // The snapshot is taken when the forwarder reads Handlers, so a handler removed mid-dispatch
        // still runs this round. That is the standard C# event contract, not a defect.
        var slot = new Hook().NewSlot();
        var seen = new List<string>();
        Action second = () => seen.Add("second");

        slot.Add(() =>
        {
            seen.Add("first");
            slot.Remove(second);
        });
        slot.Add(second);

        slot.Handlers?.Invoke();

        Assert.Equal(new[] { "first", "second" }, seen);
    }

    [Fact]
    public void FailingInstall_RollsTheHandlerBackAndSurfacesTheException()
    {
        var hook = new Hook { InstallThrows = true };
        var slot = hook.NewSlot();

        Assert.Throws<InvalidOperationException>(() => slot.Add(() => { }));

        Assert.False(slot.Installed);
        Assert.Null(slot.Handlers);
    }

    [Fact]
    public void RemovingTheLastHandlerUninstallsTheHook()
    {
        var hook = new Hook();
        var slot = hook.NewSlot();
        Action handler = () => { };
        slot.Add(handler);

        slot.Remove(handler);

        Assert.Equal(1, hook.Uninstalls);
        Assert.False(hook.Live);
        Assert.False(slot.Installed);
        Assert.Null(slot.Handlers);
    }

    [Fact]
    public void RemovingOneOfTwoHandlersKeepsTheHookInstalled()
    {
        var hook = new Hook();
        var slot = hook.NewSlot();
        Action first = () => { };
        slot.Add(first);
        slot.Add(() => { });

        slot.Remove(first);

        Assert.Equal(0, hook.Uninstalls);
        Assert.True(slot.Installed);
    }

    [Fact]
    public void RemovingAHandlerThatWasNeverAddedDoesNotUninstall()
    {
        var hook = new Hook();
        var slot = hook.NewSlot();
        slot.Add(() => { });

        slot.Remove(() => { });

        Assert.Equal(0, hook.Uninstalls);
        Assert.True(slot.Installed);
    }

    [Fact]
    public void SubscribeUnsubscribeCyclesReinstallEveryTime()
    {
        var hook = new Hook();
        var slot = hook.NewSlot();

        for (int i = 0; i < 3; i++)
        {
            Action handler = () => { };
            slot.Add(handler);
            Assert.True(hook.Live);
            slot.Remove(handler);
            Assert.False(hook.Live);
        }

        Assert.Equal(3, hook.Installs);
        Assert.Equal(3, hook.Uninstalls);
    }

    [Fact]
    public void ReleaseAfterTheLastRemoveDoesNotUninstallTwice()
    {
        var hook = new Hook();
        var slot = hook.NewSlot();
        Action handler = () => { };
        slot.Add(handler);
        slot.Remove(handler);

        slot.Release();

        Assert.Equal(1, hook.Uninstalls);
    }

    [Fact]
    public void LastHandlerUnsubscribingItselfDuringDispatch_UninstallsWithoutBreakingTheDispatch()
    {
        var hook = new Hook();
        var slot = hook.NewSlot();
        var ran = 0;

        Action? only = null;
        only = () =>
        {
            ran++;
            slot.Remove(only);
        };
        slot.Add(only);

        slot.Handlers?.Invoke();

        Assert.Equal(1, ran);
        Assert.Equal(1, hook.Uninstalls);
        Assert.False(slot.Installed);
    }

    [Fact]
    public void FailingUninstallOnTheLastRemoveIsReportedAndDoesNotEscapeToTheSubscriber()
    {
        var hook = new Hook { UninstallThrows = true };
        var slot = hook.NewSlot();
        Action handler = () => { };
        slot.Add(handler);

        slot.Remove(handler);

        Assert.False(slot.Installed);
        Assert.Equal(1, hook.Uninstalls);

        // The counter above only proves the uninstall was attempted. What matters to a user
        // reporting "it just stopped updating" is that the failure reached a log at all.
        Assert.True(hook.Trace.Reported("uninstall an event hook"));
        Assert.IsType<InvalidOperationException>(hook.Trace.Reports.Single().Error);
    }

    [Fact]
    public void Release_DropsHandlersAndUninstallsOnce()
    {
        var hook = new Hook();
        var slot = hook.NewSlot();
        slot.Add(() => { });

        slot.Release();
        slot.Release();

        Assert.Equal(1, hook.Uninstalls);
        Assert.False(hook.Live);
        Assert.False(slot.Installed);
        Assert.Null(slot.Handlers);
    }

    [Fact]
    public void Release_SwallowsAFailingUninstallSoTeardownContinues()
    {
        var hook = new Hook { UninstallThrows = true };
        var slot = hook.NewSlot();
        slot.Add(() => { });

        slot.Release();

        Assert.False(slot.Installed);
        Assert.Equal(1, hook.Uninstalls);
        Assert.True(hook.Trace.Reported("uninstall an event hook"));
        Assert.IsType<InvalidOperationException>(hook.Trace.Reports.Single().Error);
    }

    [Fact]
    public void AHandlerThatRemovesThenReAddsItselfDuringDispatch_ReinstallsTheHook()
    {
        // Exactly the manager's own idle tick: the only handler unsubscribes at the top -- which
        // takes the hook down -- and something running underneath it arms again before the
        // dispatch returns. If the slot did not re-install here, idle would be dead for good.
        var hook = new Hook();
        var slot = hook.NewSlot();
        var runs = 0;
        var installedAfterSelfRemoval = true;

        Action? tick = null;
        tick = () =>
        {
            runs++;
            slot.Remove(tick!);
            if (runs == 1)
            {
                installedAfterSelfRemoval = slot.Installed;
                slot.Add(tick!);
            }
        };
        slot.Add(tick);

        slot.Handlers?.Invoke();

        Assert.Equal(1, runs);
        Assert.False(installedAfterSelfRemoval);
        Assert.True(slot.Installed);
        Assert.True(hook.Live);
        Assert.Equal(2, hook.Installs);
        Assert.Equal(1, hook.Uninstalls);

        // The re-arm took, so the next dispatch runs it again -- and this time it does not re-add.
        slot.Handlers?.Invoke();
        Assert.Equal(2, runs);
        Assert.False(slot.Installed);
        Assert.Equal(2, hook.Uninstalls);
    }

    [Fact]
    public void ALaterHandlerInTheSameSnapshotCanReinstallAHookTheFirstOneTookDown()
    {
        var hook = new Hook();
        var slot = hook.NewSlot();
        var seen = new List<string>();
        Action replacement = () => seen.Add("replacement");

        Action? first = null;
        Action? second = null;
        first = () =>
        {
            seen.Add("first");
            slot.Remove(first!);
            slot.Remove(second!);   // the last handler goes: the hook comes down here
        };
        second = () =>
        {
            seen.Add("second");
            slot.Add(replacement);  // and goes back up here, in the same dispatch
        };
        slot.Add(first);
        slot.Add(second);

        slot.Handlers?.Invoke();

        Assert.Equal(new[] { "first", "second" }, seen);
        Assert.Equal(1, hook.Uninstalls);
        Assert.Equal(2, hook.Installs);
        Assert.True(slot.Installed);
        Assert.True(hook.Live);

        seen.Clear();
        slot.Handlers?.Invoke();
        Assert.Equal(new[] { "replacement" }, seen);
    }

    [Fact]
    public void AddAfterAFailedAddInstallsOnceTheInstallStopsFailing()
    {
        var hook = new Hook { InstallThrows = true };
        var slot = hook.NewSlot();
        Assert.Throws<InvalidOperationException>(() => slot.Add(() => { }));

        hook.InstallThrows = false;
        Action handler = () => { };
        slot.Add(handler);

        Assert.True(slot.Installed);
        Assert.True(hook.Live);
        Assert.Equal(1, hook.Installs);

        // Only the second handler is subscribed -- the rolled-back one left nothing behind.
        slot.Remove(handler);
        Assert.Null(slot.Handlers);
        Assert.False(slot.Installed);
    }

    [Fact]
    public void ReleaseFromInsideADispatchOfTheSameSlotUninstallsAndLetsTheSnapshotFinish()
    {
        var hook = new Hook();
        var slot = hook.NewSlot();
        var seen = new List<string>();

        slot.Add(() =>
        {
            seen.Add("first");
            slot.Release();
        });
        slot.Add(() => seen.Add("second"));

        slot.Handlers?.Invoke();

        // Standard event semantics: the snapshot the forwarder is holding runs to the end.
        Assert.Equal(new[] { "first", "second" }, seen);
        Assert.False(slot.Installed);
        Assert.False(hook.Live);
        Assert.Equal(1, hook.Uninstalls);
        Assert.Null(slot.Handlers);

        seen.Clear();
        slot.Handlers?.Invoke();
        Assert.Empty(seen);

        // And the slot is still usable afterwards.
        slot.Add(() => seen.Add("later"));
        Assert.True(slot.Installed);
        Assert.Equal(2, hook.Installs);
    }

    [Fact]
    public void NullHandlersAreIgnored()
    {
        var hook = new Hook();
        var slot = hook.NewSlot();

        slot.Add(null);
        Assert.False(slot.Installed);
        Assert.Equal(0, hook.Installs);

        slot.Remove(null);
        Assert.False(slot.Installed);
    }
}
