#nullable enable

// The host namespaces AcadEventManager.cs names its types through. Global, so their names are ones no consumer
// uses for anything else.
global using EventHostApp = Bricscad.ApplicationServices;
global using EventHostDb = Teigha.DatabaseServices;

using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

using Bricscad.ApplicationServices;

namespace EventManager
{
    /// <summary>
    /// BricsCAD's part of <see cref="AcadEventManager"/>: the idle source behind <see cref="RunOnNextIdle"/>. The
    /// AutoCAD part is <c>Acad\AcadEventManager.Host.cs</c>, which also has the seven application events BricsCAD
    /// lacks (customization mode, the customize, drafting-settings and options dialogs, modal enter and leave).
    /// </summary>
    /// <remarks>
    /// <para>
    /// BricsCAD's <c>Application.Idle</c> cannot be relied on: it comes from MFC's idle handler, which returns
    /// early while the input queue holds anything, and a BricsCAD started by a script raised it 0 times in 18 s at
    /// an idle prompt (X: bricscad-porting.md &lt;main-thread&gt;, measured). So the queue rides a timer on the
    /// main thread's dispatcher, which always runs. A tick drains the queue only in application context with no
    /// modal loop up, the moment AutoCAD's Idle stands for; otherwise it waits for the next tick. The same gate
    /// as NorsynDrawingTools' <c>IdleSignal</c> and DevReload's main-thread dispatcher.
    /// </para>
    /// <para>
    /// The timer runs only while something is queued: a running <see cref="DispatcherTimer"/> is rooted by the
    /// dispatcher and would keep an unloading plugin in memory. <see cref="Dispose"/> stops it through
    /// <c>IdleDrain.Abandon</c>.
    /// </para>
    /// </remarks>
    public partial class AcadEventManager
    {
        private static readonly TimeSpan IdlePoll = TimeSpan.FromMilliseconds(100);

        private DispatcherTimer? _idleTimer;

        private void ArmIdle()
        {
            // Made on first use, on the main thread (RunOnNextIdle's contract), so it ticks there.
            if (_idleTimer == null)
            {
                _idleTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = IdlePoll };
                _idleTimer.Tick += OnIdlePoll;
            }
            _idleTimer.Start();
        }

        private void DisarmIdle() => _idleTimer?.Stop();

        private void OnIdlePoll(object? sender, EventArgs e)
        {
            if (CanRunNow()) _idleDrain.Tick();
        }

        // In application context (no command holds a document) and not inside a modal loop. A modal loop is up
        // when the main frame is disabled: every modal loop (MFC DoModal, WinForms/WPF ShowDialog, a task dialog)
        // disables its owner while it runs.
        private static bool CanRunNow() =>
            Application.DocumentManager.IsApplicationContext && IsWindowEnabled(Application.MainWindow.Handle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowEnabled(IntPtr window);
    }
}
