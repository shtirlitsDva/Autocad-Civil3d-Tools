#nullable enable

using System;

namespace EventManager
{
    /// <summary>
    /// Keeps one set of handlers attached to at most one target at a time, and moves that
    /// attachment from target to target atomically. The manager uses it to follow the active
    /// document's database.
    /// </summary>
    /// <typeparam name="TTarget">
    /// The thing handlers attach to. Generic only so this type stays free of the AutoCAD
    /// assemblies and can be exercised without them; the manager closes it over
    /// <c>Autodesk.AutoCAD.DatabaseServices.Database</c>.
    /// </typeparam>
    /// <remarks>
    /// Both halves of a rebind are hazardous, and for the same reason: AutoCAD hands over
    /// documents that are already part way through teardown. Detaching from the previous target
    /// can throw, and so can attaching to the new one. Neither may escape, because the callers are
    /// AutoCAD's own document-lifecycle dispatch, where an exception is a fatal-error dialog.
    /// <para>
    /// A half-completed attach must also not leave <see cref="Bound"/> naming the target. The
    /// identity short-circuit at the top of <see cref="BindTo"/> would then turn every later
    /// rebind into a no-op, and the handlers that never attached would stay silently dead for the
    /// life of the manager. So the field is assigned only after a complete attach.
    /// </para>
    /// <para>Not thread safe. Expected to be used on the AutoCAD main thread.</para>
    /// </remarks>
    internal sealed class BoundHook<TTarget> where TTarget : class
    {
        private readonly Action<TTarget> _attach;
        private readonly Action<TTarget> _detach;
        private readonly EventManagerTrace _trace;
        private TTarget? _bound;

        internal BoundHook(Action<TTarget> attach, Action<TTarget> detach, EventManagerTrace trace)
        {
            _attach = attach ?? throw new ArgumentNullException(nameof(attach));
            _detach = detach ?? throw new ArgumentNullException(nameof(detach));
            _trace = trace ?? throw new ArgumentNullException(nameof(trace));
        }

        /// <summary>
        /// The target currently attached, or <c>null</c>. Only ever names a fully attached target.
        /// </summary>
        internal TTarget? Bound => _bound;

        /// <summary>
        /// Detaches from the current target, if any, and attaches to <paramref name="target"/>.
        /// Never throws: a failure on either half is reported and the binding is left in a state a
        /// later call can recover from.
        /// </summary>
        internal void BindTo(TTarget? target)
        {
            if (ReferenceEquals(_bound, target)) return;

            var previous = _bound;

            // Let go of the field before touching either target, so nothing that happens below can
            // leave it naming a target that is only partly hooked -- including a dead one whose
            // detach threw, which is worse to keep than the handler references it leaks.
            _bound = null;

            if (previous != null)
            {
                _trace.Guard(
                    "failed to detach from the previously bound target",
                    () => _detach(previous));
            }

            if (target == null) return;

            try
            {
                _attach(target);
            }
            catch (Exception ex)
            {
                _trace.Report("failed to attach to the new target", ex);

                // Undo whatever got through before the throw. Detaching a handler that was never
                // attached is a no-op, so running the whole detach over the target is safe.
                _trace.Guard("failed to unwind a partial attach", () => _detach(target));
                return;
            }

            _bound = target;
        }
    }
}
