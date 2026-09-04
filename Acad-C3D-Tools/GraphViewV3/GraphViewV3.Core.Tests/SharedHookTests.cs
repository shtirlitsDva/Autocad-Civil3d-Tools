using EventManager;

using Xunit;

namespace GraphViewV3.Core.Tests;

/// <summary>
/// Covers the hook several event slots share -- in the manager, the document-lifecycle
/// subscriptions behind the three ActiveObject* events. Install and uninstall are injected as
/// actions, so this runs without AutoCAD.
/// </summary>
public class SharedHookTests
{
    private sealed class Shared
    {
        public int Installs;
        public int Uninstalls;
        public bool Live;
        public bool InstallThrows;
        public bool UninstallThrows;
        public bool StillNeeded;
        public readonly RecordingTrace Trace = new();

        public SharedHook New() => new(Install, Uninstall, () => StillNeeded, Trace.Trace);

        public SharedHook NewWith(Func<bool> stillNeeded)
            => new(Install, Uninstall, stillNeeded, Trace.Trace);

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
    public void EnsureInstallsOnceAndIsIdempotent()
    {
        var shared = new Shared();
        var hook = shared.New();

        hook.Ensure();
        hook.Ensure();

        Assert.Equal(1, shared.Installs);
        Assert.True(shared.Live);
        Assert.True(hook.Installed);
    }

    [Fact]
    public void AFailingInstallLeavesTheHookNotInstalled_AndEnsureStillRetryable()
    {
        // The regression: with the flag set before the install ran, a single failure here made
        // ReleaseIfUnused unreachable and every later Ensure a silent no-op -- the events the hook
        // feeds would have been dead for the life of the manager.
        var shared = new Shared { InstallThrows = true };
        var hook = shared.New();

        Assert.Throws<InvalidOperationException>(hook.Ensure);
        Assert.False(hook.Installed);
        Assert.False(shared.Live);

        // Releasing after a failed install must not run the uninstall of a hook never installed.
        hook.ReleaseIfUnused();
        Assert.Equal(0, shared.Uninstalls);

        shared.InstallThrows = false;
        hook.Ensure();

        Assert.True(hook.Installed);
        Assert.Equal(1, shared.Installs);
        Assert.True(shared.Live);
    }

    [Fact]
    public void ReleaseIfUnusedKeepsTheHookWhileASiblingStillNeedsIt()
    {
        var shared = new Shared { StillNeeded = true };
        var hook = shared.New();
        hook.Ensure();

        hook.ReleaseIfUnused();

        Assert.Equal(0, shared.Uninstalls);
        Assert.True(hook.Installed);
        Assert.True(shared.Live);
    }

    [Fact]
    public void ReleaseIfUnusedUninstallsOnceNoSiblingNeedsIt()
    {
        var shared = new Shared { StillNeeded = true };
        var hook = shared.New();
        hook.Ensure();
        hook.ReleaseIfUnused();

        shared.StillNeeded = false;
        hook.ReleaseIfUnused();

        Assert.Equal(1, shared.Uninstalls);
        Assert.False(hook.Installed);
        Assert.False(shared.Live);
    }

    [Fact]
    public void ReleaseIgnoresTheSiblingsSoTeardownCannotBeBlocked()
    {
        // Dispose's belt and braces: it must not depend on the siblings having been released in
        // the right order to get the hook down.
        var shared = new Shared { StillNeeded = true };
        var hook = shared.New();
        hook.Ensure();

        hook.Release();

        Assert.Equal(1, shared.Uninstalls);
        Assert.False(hook.Installed);
        Assert.False(shared.Live);
    }

    [Fact]
    public void ReleaseTwiceUninstallsOnce()
    {
        var shared = new Shared();
        var hook = shared.New();
        hook.Ensure();

        hook.Release();
        hook.Release();

        Assert.Equal(1, shared.Uninstalls);
    }

    [Fact]
    public void EnsureReleaseCyclesReinstallEveryTime()
    {
        var shared = new Shared();
        var hook = shared.New();

        for (int i = 0; i < 3; i++)
        {
            hook.Ensure();
            Assert.True(shared.Live);
            hook.ReleaseIfUnused();
            Assert.False(shared.Live);
        }

        Assert.Equal(3, shared.Installs);
        Assert.Equal(3, shared.Uninstalls);
    }

    [Fact]
    public void AFailingUninstallIsReportedAndLeavesTheHookReArmable()
    {
        var shared = new Shared { UninstallThrows = true };
        var hook = shared.New();
        hook.Ensure();

        hook.Release();

        Assert.False(hook.Installed);
        Assert.True(shared.Trace.Reported("uninstall a shared event hook"));
        Assert.IsType<InvalidOperationException>(shared.Trace.Reports.Single().Error);

        hook.Ensure();
        Assert.True(hook.Installed);
        Assert.Equal(2, shared.Installs);
    }

    // ---- the sibling interleavings, driven through the real HookSlots ----

    private static (SharedHook Hook, Shared Probe, HookSlot<Action>[] Slots) ThreeSiblings()
    {
        var probe = new Shared();
        HookSlot<Action>? a = null, b = null, c = null;

        var shared = probe.NewWith(() =>
            (a?.Installed ?? false) || (b?.Installed ?? false) || (c?.Installed ?? false));

        a = new HookSlot<Action>(shared.Ensure, shared.ReleaseIfUnused, probe.Trace.Trace);
        b = new HookSlot<Action>(shared.Ensure, shared.ReleaseIfUnused, probe.Trace.Trace);
        c = new HookSlot<Action>(shared.Ensure, shared.ReleaseIfUnused, probe.Trace.Trace);

        return (shared, probe, new[] { a, b, c });
    }

    [Fact]
    public void TheFirstSiblingToSubscribeInstallsTheSharedHook_TheOthersDoNot()
    {
        var (hook, probe, slots) = ThreeSiblings();

        slots[0].Add(() => { });
        slots[1].Add(() => { });
        slots[2].Add(() => { });

        Assert.Equal(1, probe.Installs);
        Assert.True(hook.Installed);
    }

    [Fact]
    public void TheSharedHookSurvivesUntilTheLastSiblingUnsubscribes()
    {
        var (hook, probe, slots) = ThreeSiblings();
        Action h0 = () => { }, h1 = () => { }, h2 = () => { };
        slots[0].Add(h0);
        slots[1].Add(h1);
        slots[2].Add(h2);

        slots[0].Remove(h0);
        Assert.Equal(0, probe.Uninstalls);
        Assert.True(hook.Installed);

        slots[2].Remove(h2);
        Assert.Equal(0, probe.Uninstalls);
        Assert.True(hook.Installed);

        slots[1].Remove(h1);
        Assert.Equal(1, probe.Uninstalls);
        Assert.False(hook.Installed);
        Assert.False(probe.Live);
    }

    [Fact]
    public void TheSharedHookIsReInstalledWhenASiblingSubscribesAgain()
    {
        var (hook, probe, slots) = ThreeSiblings();

        for (int i = 0; i < 3; i++)
        {
            Action handler = () => { };
            var slot = slots[i];
            slot.Add(handler);
            Assert.True(probe.Live);
            slot.Remove(handler);
            Assert.False(probe.Live);
        }

        Assert.Equal(3, probe.Installs);
        Assert.Equal(3, probe.Uninstalls);
        Assert.False(hook.Installed);
    }

    [Fact]
    public void ASiblingWhoseInstallFailedDoesNotStrandTheSharedHook()
    {
        // MAJOR 3, end to end: the slot rolls its own state back, and because the shared hook
        // never claimed to be installed, the next subscription installs it for real.
        var (hook, probe, slots) = ThreeSiblings();
        probe.InstallThrows = true;

        Assert.Throws<InvalidOperationException>(() => slots[0].Add(() => { }));

        Assert.False(slots[0].Installed);
        Assert.False(hook.Installed);
        Assert.Null(slots[0].Handlers);

        probe.InstallThrows = false;
        slots[1].Add(() => { });

        Assert.True(hook.Installed);
        Assert.Equal(1, probe.Installs);
        Assert.True(probe.Live);
    }
}
