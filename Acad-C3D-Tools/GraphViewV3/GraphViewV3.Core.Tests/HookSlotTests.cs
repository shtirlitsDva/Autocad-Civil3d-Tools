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

        public HookSlot<Action> NewSlot() => new(Install, Uninstall);

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
