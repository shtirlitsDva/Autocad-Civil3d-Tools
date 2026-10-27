#nullable enable

using System;
using System.Collections.Generic;

// No host namespace here: this file builds for AutoCAD and for BricsCAD. It names the host's types through
// EventHostApp and EventHostDb, which the host part (Acad\ or Brx\AcadEventManager.Host.cs) declares.

namespace EventManager
{
    /// <summary>
    /// Multiplexes AutoCAD application, document-collection and active-document database events
    /// behind one instance whose lifetime a plugin controls, and tracks per-document unsubscribe
    /// callbacks so a closing drawing releases everything hooked against it.
    /// </summary>
    /// <remarks>
    /// Every exposed event is backed by a <see cref="HookSlot{THandler}"/>: the underlying AutoCAD
    /// hook is installed the first time a handler subscribes and uninstalled again when the last
    /// one unsubscribes, so the manager holds only the hooks somebody is actually listening to.
    /// The handlers themselves live in a multicast delegate, so dispatch reads an immutable
    /// snapshot and a handler is free to subscribe or unsubscribe from inside another handler.
    /// <para>
    /// Subscribing after <see cref="Dispose"/> throws <see cref="ObjectDisposedException"/>: the
    /// hook it installed would have no owner left to release it. Unsubscribing stays safe.
    /// </para>
    /// <para>
    /// Not thread safe. Every member is expected to be used on the AutoCAD main thread.
    /// </para>
    /// </remarks>
    public partial class AcadEventManager : IDisposable
    {
        /// <summary>
        /// Unsubscribe callbacks parked by a plugin against a document, released together when
        /// that document is destroyed.
        /// </summary>
        private readonly SubscriptionLedger<EventHostApp.Document> _subscriptions;

        /// <summary>
        /// Every slot created so far, in creation order, so <see cref="Dispose"/> can release the
        /// underlying hooks. Slots are appended once, on the first subscription to their event.
        /// </summary>
        private readonly List<IHookSlot> _slots = new();

        private readonly EventManagerTrace _trace;
        private bool _disposed;

        /// <param name="log">
        /// Where to report a failure the manager swallowed -- a hook that would not uninstall, a
        /// database that would not detach, an action queued for idle that threw. Those are the
        /// failures a user experiences as "it just stopped updating", so a plugin should pass its
        /// own logger here. Left null they go to <see cref="System.Diagnostics.Trace"/>, which in
        /// a Release AutoCAD process reaches only a native debugger.
        /// </param>
        public AcadEventManager(Action<string, Exception?>? log = null)
        {
            _trace = new EventManagerTrace(log);
            _subscriptions = new SubscriptionLedger<EventHostApp.Document>(_trace);
            _idleDrain = new IdleDrain(ArmIdle, DisarmIdle, _trace);
            _dbBinding = new BoundHook<EventHostDb.Database>(
                AttachToDatabase, DetachFromDatabase, _trace);
            _dbHook = new SharedHook(
                InstallDbHook, UninstallDbHook, DbHookStillNeeded, _trace);

            EventHostApp.Application.DocumentManager.DocumentToBeDestroyed += OnDocToBeDestroyed;
        }

        public void Track(EventHostApp.Document doc, Action unsubscribe)
            => _subscriptions.Track(doc, unsubscribe);

        public IReadOnlyDictionary<EventHostApp.Document, int> GetSubscriptions()
            => _subscriptions.Counts();

        public bool HasSubscriptions(EventHostApp.Document doc) => _subscriptions.Has(doc);

        public int GetSubscriptionCount(EventHostApp.Document doc) => _subscriptions.CountFor(doc);

        /// <summary>
        /// How many underlying AutoCAD hooks this manager is holding installed right now:
        /// one per event somebody is subscribed to, plus the shared database hook and the
        /// active-document database binding when those are up. Zero before the first
        /// subscription, and zero again after <see cref="Dispose"/>.
        /// </summary>
        /// <remarks>
        /// A teardown probe, not bookkeeping any consumer routes through -- its point is that
        /// "the plugin unloaded and left nothing hooked" can be asserted rather than reasoned
        /// about. <see cref="GetSubscriptions"/> answers a different question (how many
        /// unsubscribe callbacks a plugin has parked against each document) and has never
        /// known anything about these hooks, which is what made it the wrong instrument when
        /// it was named for this job.
        /// </remarks>
        public int InstalledHookCount
        {
            get
            {
                int count = 0;
                if (_dbHook.Installed) count++;
                if (_dbBinding.Bound != null) count++;
                foreach (IHookSlot slot in _slots) if (slot.Installed) count++;
                return count;
            }
        }

        /// <summary>
        /// Returns the slot backing one event, creating and registering it on first use.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The manager has been disposed. Subscribing afterwards would install an AutoCAD hook
        /// that nothing is left to uninstall, so it is refused rather than silently leaked.
        /// </exception>
        private HookSlot<THandler> Slot<THandler>(
            ref HookSlot<THandler>? slot, Action install, Action uninstall)
            where THandler : Delegate
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AcadEventManager));

            if (slot == null)
            {
                slot = new HookSlot<THandler>(install, uninstall, _trace);
                _slots.Add(slot);
            }
            return slot;
        }

        #region One-Shot Idle

        /// <summary>
        /// The queue and its arm/disarm bookkeeping. Assigned in the constructor. The host part arms it:
        /// AutoCAD's through this manager's own <see cref="Idle"/> event, BricsCAD's through a timer.
        /// </summary>
        private readonly IdleDrain _idleDrain;

        /// <summary>
        /// Runs <paramref name="action"/> once, on the next <see cref="Idle"/>, and then lets go
        /// of the idle hook again unless something is still queued.
        /// </summary>
        /// <remarks>
        /// This is the arm-work-disarm debounce as a shared primitive: arm when work appears,
        /// detach as soon as it has been done, so an idle tick costs nothing while there is
        /// nothing to do. It replaces a hand-rolled <c>Application.Idle += / -=</c> pair and
        /// nothing more -- the arming policy stays with the caller.
        /// <para>The contract, in full, because callers depend on all of it:</para>
        /// <list type="bullet">
        /// <item><description>
        /// <b>One shot.</b> The action runs exactly once per call and is then forgotten. Queue it
        /// again to have it run again.
        /// </description></item>
        /// <item><description>
        /// <b>No de-duplication.</b> Two calls with the same action run it twice. A caller that
        /// arms on every change keeps its own "already armed" flag, exactly as it did around the
        /// raw pair.
        /// </description></item>
        /// <item><description>
        /// <b>FIFO, and one generation per tick.</b> Everything queued before a tick runs on that
        /// tick, in queue order. Anything queued from inside the tick runs on a later one.
        /// </description></item>
        /// <item><description>
        /// <b>Isolated.</b> An exception out of one action is reported and swallowed -- it must
        /// not reach AutoCAD's message pump -- and the actions queued behind it still run.
        /// </description></item>
        /// <item><description>
        /// <b>Re-entrancy.</b> An action that pumps messages (a modal dialog does) makes AutoCAD
        /// raise Idle again while the drain is still on the stack. That nested tick returns
        /// immediately <i>without</i> consuming the arming, so work queued from inside the modal
        /// still runs on a later real idle. It does <i>not</i> give an action mutual exclusion
        /// with itself: a caller whose own pass must not re-enter keeps its own re-entrancy guard.
        /// </description></item>
        /// <item><description>
        /// <b>No cancellation.</b> A queued action cannot be withdrawn. A caller that may be torn
        /// down before the tick checks its own disposed flag at the top of the action.
        /// </description></item>
        /// </list>
        /// <para>
        /// Runs on the AutoCAD main thread, like every other member here, and is not thread safe.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">The manager has been disposed.</exception>
        public void RunOnNextIdle(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (_disposed) throw new ObjectDisposedException(nameof(AcadEventManager));

            _idleDrain.Enqueue(action);
        }


        #endregion

        #region Application Events


        private HookSlot<EventHandler<EventHostApp.BeginDoubleClickEventArgs>>? _beginDoubleClick;
        public event EventHandler<EventHostApp.BeginDoubleClickEventArgs> BeginDoubleClick
        {
            add => Slot(
                ref _beginDoubleClick,
                () => EventHostApp.Application.BeginDoubleClick += FwdBeginDoubleClick,
                () => EventHostApp.Application.BeginDoubleClick -= FwdBeginDoubleClick).Add(value);
            remove => _beginDoubleClick?.Remove(value);
        }
        private void FwdBeginDoubleClick(object? s, EventHostApp.BeginDoubleClickEventArgs e) => _beginDoubleClick?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler>? _beginQuit;
        public event EventHandler BeginQuit
        {
            add => Slot(
                ref _beginQuit,
                () => EventHostApp.Application.BeginQuit += FwdBeginQuit,
                () => EventHostApp.Application.BeginQuit -= FwdBeginQuit).Add(value);
            remove => _beginQuit?.Remove(value);
        }
        private void FwdBeginQuit(object? s, EventArgs e) => _beginQuit?.Handlers?.Invoke(s, e);






        private HookSlot<EventHandler>? _idle;
        public event EventHandler Idle
        {
            add => Slot(
                ref _idle,
                () => EventHostApp.Application.Idle += FwdIdle,
                () => EventHostApp.Application.Idle -= FwdIdle).Add(value);
            remove => _idle?.Remove(value);
        }
        private void FwdIdle(object? s, EventArgs e) => _idle?.Handlers?.Invoke(s, e);


        private HookSlot<EventHandler<EventHostApp.PreTranslateMessageEventArgs>>? _preTranslateMessage;
        public event EventHandler<EventHostApp.PreTranslateMessageEventArgs> PreTranslateMessage
        {
            add => Slot(
                ref _preTranslateMessage,
                () => EventHostApp.Application.PreTranslateMessage += FwdPreTranslateMessage,
                () => EventHostApp.Application.PreTranslateMessage -= FwdPreTranslateMessage).Add(value);
            remove => _preTranslateMessage?.Remove(value);
        }
        private void FwdPreTranslateMessage(object? s, EventHostApp.PreTranslateMessageEventArgs e) => _preTranslateMessage?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler>? _quitAborted;
        public event EventHandler QuitAborted
        {
            add => Slot(
                ref _quitAborted,
                () => EventHostApp.Application.QuitAborted += FwdQuitAborted,
                () => EventHostApp.Application.QuitAborted -= FwdQuitAborted).Add(value);
            remove => _quitAborted?.Remove(value);
        }
        private void FwdQuitAborted(object? s, EventArgs e) => _quitAborted?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler>? _quitWillStart;
        public event EventHandler QuitWillStart
        {
            add => Slot(
                ref _quitWillStart,
                () => EventHostApp.Application.QuitWillStart += FwdQuitWillStart,
                () => EventHostApp.Application.QuitWillStart -= FwdQuitWillStart).Add(value);
            remove => _quitWillStart?.Remove(value);
        }
        private void FwdQuitWillStart(object? s, EventArgs e) => _quitWillStart?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<EventHostApp.SystemVariableChangedEventArgs>>? _systemVariableChanged;
        public event EventHandler<EventHostApp.SystemVariableChangedEventArgs> SystemVariableChanged
        {
            add => Slot(
                ref _systemVariableChanged,
                () => EventHostApp.Application.SystemVariableChanged += FwdSystemVariableChanged,
                () => EventHostApp.Application.SystemVariableChanged -= FwdSystemVariableChanged).Add(value);
            remove => _systemVariableChanged?.Remove(value);
        }
        private void FwdSystemVariableChanged(object? s, EventHostApp.SystemVariableChangedEventArgs e) => _systemVariableChanged?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<EventHostApp.SystemVariableChangingEventArgs>>? _systemVariableChanging;
        public event EventHandler<EventHostApp.SystemVariableChangingEventArgs> SystemVariableChanging
        {
            add => Slot(
                ref _systemVariableChanging,
                () => EventHostApp.Application.SystemVariableChanging += FwdSystemVariableChanging,
                () => EventHostApp.Application.SystemVariableChanging -= FwdSystemVariableChanging).Add(value);
            remove => _systemVariableChanging?.Remove(value);
        }
        private void FwdSystemVariableChanging(object? s, EventHostApp.SystemVariableChangingEventArgs e) => _systemVariableChanging?.Handlers?.Invoke(s, e);

        #endregion

        #region DocumentCollection Events

        private HookSlot<EventHandler<EventHostApp.DocumentCollectionEventArgs>>? _documentActivated;
        public event EventHandler<EventHostApp.DocumentCollectionEventArgs> DocumentActivated
        {
            add => Slot(
                ref _documentActivated,
                () => EventHostApp.Application.DocumentManager.DocumentActivated += FwdDocumentActivated,
                () => EventHostApp.Application.DocumentManager.DocumentActivated -= FwdDocumentActivated).Add(value);
            remove => _documentActivated?.Remove(value);
        }
        private void FwdDocumentActivated(object? s, EventHostApp.DocumentCollectionEventArgs e) => _documentActivated?.Handlers?.Invoke(s, e);

        private HookSlot<EventHostApp.DocumentActivationChangedEventHandler>? _documentActivationChanged;
        public event EventHostApp.DocumentActivationChangedEventHandler DocumentActivationChanged
        {
            add => Slot(
                ref _documentActivationChanged,
                () => EventHostApp.Application.DocumentManager.DocumentActivationChanged += FwdDocumentActivationChanged,
                () => EventHostApp.Application.DocumentManager.DocumentActivationChanged -= FwdDocumentActivationChanged).Add(value);
            remove => _documentActivationChanged?.Remove(value);
        }
        private void FwdDocumentActivationChanged(object? s, EventHostApp.DocumentActivationChangedEventArgs e) => _documentActivationChanged?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<EventHostApp.DocumentCollectionEventArgs>>? _documentBecameCurrent;
        public event EventHandler<EventHostApp.DocumentCollectionEventArgs> DocumentBecameCurrent
        {
            add => Slot(
                ref _documentBecameCurrent,
                () => EventHostApp.Application.DocumentManager.DocumentBecameCurrent += FwdDocumentBecameCurrent,
                () => EventHostApp.Application.DocumentManager.DocumentBecameCurrent -= FwdDocumentBecameCurrent).Add(value);
            remove => _documentBecameCurrent?.Remove(value);
        }
        private void FwdDocumentBecameCurrent(object? s, EventHostApp.DocumentCollectionEventArgs e) => _documentBecameCurrent?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<EventHostApp.DocumentCollectionEventArgs>>? _documentCreated;
        public event EventHandler<EventHostApp.DocumentCollectionEventArgs> DocumentCreated
        {
            add => Slot(
                ref _documentCreated,
                () => EventHostApp.Application.DocumentManager.DocumentCreated += FwdDocumentCreated,
                () => EventHostApp.Application.DocumentManager.DocumentCreated -= FwdDocumentCreated).Add(value);
            remove => _documentCreated?.Remove(value);
        }
        private void FwdDocumentCreated(object? s, EventHostApp.DocumentCollectionEventArgs e) => _documentCreated?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<EventHostApp.DocumentCollectionEventArgs>>? _documentCreateStarted;
        public event EventHandler<EventHostApp.DocumentCollectionEventArgs> DocumentCreateStarted
        {
            add => Slot(
                ref _documentCreateStarted,
                () => EventHostApp.Application.DocumentManager.DocumentCreateStarted += FwdDocumentCreateStarted,
                () => EventHostApp.Application.DocumentManager.DocumentCreateStarted -= FwdDocumentCreateStarted).Add(value);
            remove => _documentCreateStarted?.Remove(value);
        }
        private void FwdDocumentCreateStarted(object? s, EventHostApp.DocumentCollectionEventArgs e) => _documentCreateStarted?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<EventHostApp.DocumentCollectionEventArgs>>? _documentCreationCanceled;
        public event EventHandler<EventHostApp.DocumentCollectionEventArgs> DocumentCreationCanceled
        {
            add => Slot(
                ref _documentCreationCanceled,
                () => EventHostApp.Application.DocumentManager.DocumentCreationCanceled += FwdDocumentCreationCanceled,
                () => EventHostApp.Application.DocumentManager.DocumentCreationCanceled -= FwdDocumentCreationCanceled).Add(value);
            remove => _documentCreationCanceled?.Remove(value);
        }
        private void FwdDocumentCreationCanceled(object? s, EventHostApp.DocumentCollectionEventArgs e) => _documentCreationCanceled?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<EventHostApp.DocumentDestroyedEventArgs>>? _documentDestroyed;
        public event EventHandler<EventHostApp.DocumentDestroyedEventArgs> DocumentDestroyed
        {
            add => Slot(
                ref _documentDestroyed,
                () => EventHostApp.Application.DocumentManager.DocumentDestroyed += FwdDocumentDestroyed,
                () => EventHostApp.Application.DocumentManager.DocumentDestroyed -= FwdDocumentDestroyed).Add(value);
            remove => _documentDestroyed?.Remove(value);
        }
        private void FwdDocumentDestroyed(object? s, EventHostApp.DocumentDestroyedEventArgs e) => _documentDestroyed?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<EventHostApp.DocumentLockModeChangedEventArgs>>? _documentLockModeChanged;
        public event EventHandler<EventHostApp.DocumentLockModeChangedEventArgs> DocumentLockModeChanged
        {
            add => Slot(
                ref _documentLockModeChanged,
                () => EventHostApp.Application.DocumentManager.DocumentLockModeChanged += FwdDocumentLockModeChanged,
                () => EventHostApp.Application.DocumentManager.DocumentLockModeChanged -= FwdDocumentLockModeChanged).Add(value);
            remove => _documentLockModeChanged?.Remove(value);
        }
        private void FwdDocumentLockModeChanged(object? s, EventHostApp.DocumentLockModeChangedEventArgs e) => _documentLockModeChanged?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<EventHostApp.DocumentLockModeChangeVetoedEventArgs>>? _documentLockModeChangeVetoed;
        public event EventHandler<EventHostApp.DocumentLockModeChangeVetoedEventArgs> DocumentLockModeChangeVetoed
        {
            add => Slot(
                ref _documentLockModeChangeVetoed,
                () => EventHostApp.Application.DocumentManager.DocumentLockModeChangeVetoed += FwdDocumentLockModeChangeVetoed,
                () => EventHostApp.Application.DocumentManager.DocumentLockModeChangeVetoed -= FwdDocumentLockModeChangeVetoed).Add(value);
            remove => _documentLockModeChangeVetoed?.Remove(value);
        }
        private void FwdDocumentLockModeChangeVetoed(object? s, EventHostApp.DocumentLockModeChangeVetoedEventArgs e) => _documentLockModeChangeVetoed?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<EventHostApp.DocumentLockModeWillChangeEventArgs>>? _documentLockModeWillChange;
        public event EventHandler<EventHostApp.DocumentLockModeWillChangeEventArgs> DocumentLockModeWillChange
        {
            add => Slot(
                ref _documentLockModeWillChange,
                () => EventHostApp.Application.DocumentManager.DocumentLockModeWillChange += FwdDocumentLockModeWillChange,
                () => EventHostApp.Application.DocumentManager.DocumentLockModeWillChange -= FwdDocumentLockModeWillChange).Add(value);
            remove => _documentLockModeWillChange?.Remove(value);
        }
        private void FwdDocumentLockModeWillChange(object? s, EventHostApp.DocumentLockModeWillChangeEventArgs e) => _documentLockModeWillChange?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<EventHostApp.DocumentCollectionEventArgs>>? _documentToBeActivated;
        public event EventHandler<EventHostApp.DocumentCollectionEventArgs> DocumentToBeActivated
        {
            add => Slot(
                ref _documentToBeActivated,
                () => EventHostApp.Application.DocumentManager.DocumentToBeActivated += FwdDocumentToBeActivated,
                () => EventHostApp.Application.DocumentManager.DocumentToBeActivated -= FwdDocumentToBeActivated).Add(value);
            remove => _documentToBeActivated?.Remove(value);
        }
        private void FwdDocumentToBeActivated(object? s, EventHostApp.DocumentCollectionEventArgs e) => _documentToBeActivated?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<EventHostApp.DocumentCollectionEventArgs>>? _documentToBeDeactivated;
        public event EventHandler<EventHostApp.DocumentCollectionEventArgs> DocumentToBeDeactivated
        {
            add => Slot(
                ref _documentToBeDeactivated,
                () => EventHostApp.Application.DocumentManager.DocumentToBeDeactivated += FwdDocumentToBeDeactivated,
                () => EventHostApp.Application.DocumentManager.DocumentToBeDeactivated -= FwdDocumentToBeDeactivated).Add(value);
            remove => _documentToBeDeactivated?.Remove(value);
        }
        private void FwdDocumentToBeDeactivated(object? s, EventHostApp.DocumentCollectionEventArgs e) => _documentToBeDeactivated?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<EventHostApp.DocumentCollectionEventArgs>>? _documentToBeDestroyed;
        public event EventHandler<EventHostApp.DocumentCollectionEventArgs> DocumentToBeDestroyed
        {
            add => Slot(
                ref _documentToBeDestroyed,
                () => EventHostApp.Application.DocumentManager.DocumentToBeDestroyed += FwdDocumentToBeDestroyed,
                () => EventHostApp.Application.DocumentManager.DocumentToBeDestroyed -= FwdDocumentToBeDestroyed).Add(value);
            remove => _documentToBeDestroyed?.Remove(value);
        }
        private void FwdDocumentToBeDestroyed(object? s, EventHostApp.DocumentCollectionEventArgs e) => _documentToBeDestroyed?.Handlers?.Invoke(s, e);

        #endregion

        #region Active-Document Database Events

        // Object change notifications live on the Database, not on Application/DocumentManager.
        // These aggregate events follow the active document automatically: subscribe once and you
        // receive the active drawing's append/modify/erase events, rebinding on document switch.
        /// <summary>
        /// The document-lifecycle subscriptions the three ActiveObject* events share, installed
        /// atomically so a failure part way through leaves them re-armable.
        /// </summary>
        private readonly SharedHook _dbHook;

        /// <summary>
        /// The active document's database and the three object-event handlers attached to it.
        /// </summary>
        private readonly BoundHook<EventHostDb.Database> _dbBinding;

        private HookSlot<EventHostDb.ObjectEventHandler>? _activeObjectAppended;
        public event EventHostDb.ObjectEventHandler ActiveObjectAppended
        {
            add => Slot(ref _activeObjectAppended, EnsureDbHook, ReleaseDbHook).Add(value);
            remove => _activeObjectAppended?.Remove(value);
        }

        private HookSlot<EventHostDb.ObjectEventHandler>? _activeObjectModified;
        public event EventHostDb.ObjectEventHandler ActiveObjectModified
        {
            add => Slot(ref _activeObjectModified, EnsureDbHook, ReleaseDbHook).Add(value);
            remove => _activeObjectModified?.Remove(value);
        }

        private HookSlot<EventHostDb.ObjectErasedEventHandler>? _activeObjectErased;
        public event EventHostDb.ObjectErasedEventHandler ActiveObjectErased
        {
            add => Slot(ref _activeObjectErased, EnsureDbHook, ReleaseDbHook).Add(value);
            remove => _activeObjectErased?.Remove(value);
        }

        /// <summary>Install action of the three ActiveObject* slots.</summary>
        private void EnsureDbHook() => _dbHook.Ensure();

        /// <summary>
        /// Uninstall action of the three ActiveObject* slots: releases the shared hook once the
        /// last of them has lost its handlers. The slot that triggered this has already cleared
        /// its own Installed flag, so <see cref="DbHookStillNeeded"/> sees only the siblings that
        /// are genuinely still listening.
        /// </summary>
        private void ReleaseDbHook() => _dbHook.ReleaseIfUnused();

        private bool DbHookStillNeeded()
            => StillListening(_activeObjectAppended)
            || StillListening(_activeObjectModified)
            || StillListening(_activeObjectErased);

        private void InstallDbHook()
        {
            try
            {
                DocumentActivated += OnActiveDocChanged;
                DocumentToBeDeactivated += OnActiveDocDeactivated;
                DocumentToBeDestroyed += OnActiveDocToBeDestroyed;
                DocumentDestroyed += OnActiveDocDestroyed;
            }
            catch
            {
                // Unwind whatever got through before rethrowing. SharedHook leaves the hook
                // reported as not installed, so a later attempt starts over -- and it must not
                // find half of these subscriptions still in place and end up subscribed twice.
                DetachDocumentLifecycleHooks();
                throw;
            }

            BindDatabase(ActiveDatabase());
        }

        private void UninstallDbHook()
        {
            DetachDocumentLifecycleHooks();
            BindDatabase(null);
        }

        /// <summary>
        /// Drops the four document-lifecycle subscriptions. Unsubscribing a handler that was never
        /// subscribed is a no-op, which is what makes this usable as the rollback for a partial
        /// install as well as the teardown for a complete one.
        /// </summary>
        private void DetachDocumentLifecycleHooks()
        {
            DocumentActivated -= OnActiveDocChanged;
            DocumentToBeDeactivated -= OnActiveDocDeactivated;
            DocumentToBeDestroyed -= OnActiveDocToBeDestroyed;
            DocumentDestroyed -= OnActiveDocDestroyed;
        }

        private static bool StillListening(IHookSlot? slot) => slot != null && slot.Installed;

        /// <summary>
        /// Reads a document's database. AutoCAD raises the lifecycle events this is called from
        /// while documents are part way through teardown, so the read itself can throw -- and it
        /// must not escape, because the caller is AutoCAD's own dispatch.
        /// </summary>
        private EventHostDb.Database? DatabaseOf(EventHostApp.Document? doc, string what)
        {
            try
            {
                return doc?.Database;
            }
            catch (Exception ex)
            {
                _trace.Report(what, ex);
                return null;
            }
        }

        private EventHostDb.Database? ActiveDatabase()
        {
            EventHostApp.Document? active;
            try
            {
                active = EventHostApp.Application.DocumentManager.MdiActiveDocument;
            }
            catch (Exception ex)
            {
                _trace.Report("failed to read the active document", ex);
                return null;
            }

            return DatabaseOf(active, "failed to read the active document's database");
        }

        private void OnActiveDocChanged(object? s, EventHostApp.DocumentCollectionEventArgs e)
            => BindDatabase(DatabaseOf(
                e.Document, "failed to read the database of the document being activated"));

        private void OnActiveDocDeactivated(object? s, EventHostApp.DocumentCollectionEventArgs e)
            => BindDatabase(null);

        /// <summary>
        /// A drawing can be closed while it is still the active document, in which case no
        /// deactivate precedes the destroy and nothing else would release the binding. Let go of
        /// the database here, while unsubscribing from it is still safe.
        /// </summary>
        private void OnActiveDocToBeDestroyed(object? s, EventHostApp.DocumentCollectionEventArgs e)
        {
            var dying = DatabaseOf(
                e.Document, "failed to read the database of a document being destroyed");

            // Let go only when the dying database is positively the one held. A document that
            // cannot be identified is NOT evidence that it is ours: unbinding on that guess
            // detaches the live active drawing, and every edit on it is then silently dropped.
            // OnActiveDocDestroyed is the backstop for the unidentifiable case -- it rebinds to
            // whatever is active once the dust settles, which releases a dead database then.
            if (dying != null && ReferenceEquals(dying, _dbBinding.Bound)) BindDatabase(null);
        }

        /// <summary>
        /// The document is gone. Rebind to whatever is active now: null when the last drawing was
        /// closed, the next drawing otherwise. This is also the backstop that releases a dead
        /// database if a destroy ever arrives without either a preceding deactivate or a
        /// resolvable document.
        /// </summary>
        private void OnActiveDocDestroyed(object? s, EventHostApp.DocumentDestroyedEventArgs e)
            => BindDatabase(ActiveDatabase());

        /// <summary>
        /// Follows the active document's database. Never throws, and never ends up claiming a
        /// database it is only partly attached to -- see <see cref="BoundHook{TTarget}"/>.
        /// </summary>
        private void BindDatabase(EventHostDb.Database? db) => _dbBinding.BindTo(db);

        private void AttachToDatabase(EventHostDb.Database db)
        {
            db.ObjectAppended += FwdObjectAppended;
            db.ObjectModified += FwdObjectModified;
            db.ObjectErased += FwdObjectErased;
        }

        private void DetachFromDatabase(EventHostDb.Database db)
        {
            db.ObjectAppended -= FwdObjectAppended;
            db.ObjectModified -= FwdObjectModified;
            db.ObjectErased -= FwdObjectErased;
        }

        private void FwdObjectAppended(object? s, EventHostDb.ObjectEventArgs e)
            => _activeObjectAppended?.Handlers?.Invoke(s, e);
        private void FwdObjectModified(object? s, EventHostDb.ObjectEventArgs e)
            => _activeObjectModified?.Handlers?.Invoke(s, e);
        private void FwdObjectErased(object? s, EventHostDb.ObjectErasedEventArgs e)
            => _activeObjectErased?.Handlers?.Invoke(s, e);

        #endregion

        private void OnDocToBeDestroyed(object? sender, EventHostApp.DocumentCollectionEventArgs e)
            => _subscriptions.Release(e.Document);

        /// <summary>
        /// Releases everything the manager installed. Each step is independent: one that fails is
        /// reported and the rest still run, because a step throwing out of here would leave the
        /// hooks of an unloading plugin installed in AutoCAD permanently -- and a second
        /// <see cref="Dispose"/> would return early without retrying them.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _subscriptions.ReleaseAll();

            // Anything still queued for an idle tick is dropped, and the idle hook let go of. A
            // generation already being drained is not withdrawable and will finish running.
            _idleDrain.Abandon();

            // Index loop: releasing one slot can touch another (the database hook unsubscribes
            // itself from DocumentActivated), and _slots must tolerate that.
            for (int i = 0; i < _slots.Count; i++) _slots[i].Release();
            _slots.Clear();

            // Belt and braces. The slot releases above should already have taken these down
            // through their uninstall actions, but teardown must not depend on that chain being
            // reached: if it were not, the manager would leave a live database hook behind.
            _dbHook.Release();
            BindDatabase(null);

            _trace.Guard(
                "failed to detach the document-destroy hook",
                () => EventHostApp.Application.DocumentManager.DocumentToBeDestroyed -= OnDocToBeDestroyed);
        }
    }
}
