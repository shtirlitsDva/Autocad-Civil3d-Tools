#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.AutoCAD.ApplicationServices;

using DbServices = Autodesk.AutoCAD.DatabaseServices;

namespace EventManager
{
    /// <summary>
    /// Multiplexes AutoCAD application, document-collection and active-document database events
    /// behind one instance whose lifetime a plugin controls, and tracks per-document unsubscribe
    /// callbacks so a closing drawing releases everything hooked against it.
    /// </summary>
    /// <remarks>
    /// Every exposed event is backed by a <see cref="HookSlot{THandler}"/>: the underlying AutoCAD
    /// hook is installed the first time a handler subscribes, and the handlers themselves live in a
    /// multicast delegate, so dispatch reads an immutable snapshot and a handler is free to
    /// subscribe or unsubscribe from inside another handler.
    /// <para>
    /// Not thread safe. Every member is expected to be used on the AutoCAD main thread.
    /// </para>
    /// </remarks>
    public class AcadEventManager : IDisposable
    {
        private readonly Dictionary<Document, List<Action>> _subscriptions = new();

        /// <summary>
        /// Every slot created so far, in creation order, so <see cref="Dispose"/> can release the
        /// underlying hooks. Slots are appended once, on the first subscription to their event.
        /// </summary>
        private readonly List<IHookSlot> _slots = new();

        private bool _disposed;

        public AcadEventManager()
        {
            Application.DocumentManager.DocumentToBeDestroyed += OnDocToBeDestroyed;
        }

        public void Track(Document doc, Action unsubscribe)
        {
            if (!_subscriptions.TryGetValue(doc, out var list))
            {
                list = new List<Action>();
                _subscriptions[doc] = list;
            }
            list.Add(unsubscribe);
        }

        public IReadOnlyDictionary<Document, int> GetSubscriptions()
            => _subscriptions.ToDictionary(kv => kv.Key, kv => kv.Value.Count);

        public bool HasSubscriptions(Document doc)
            => _subscriptions.ContainsKey(doc);

        public int GetSubscriptionCount(Document doc)
            => _subscriptions.TryGetValue(doc, out var list) ? list.Count : 0;

        /// <summary>
        /// Returns the slot backing one event, creating and registering it on first use.
        /// </summary>
        private HookSlot<THandler> Slot<THandler>(
            ref HookSlot<THandler>? slot, Action install, Action uninstall)
            where THandler : Delegate
        {
            if (slot == null)
            {
                slot = new HookSlot<THandler>(install, uninstall);
                _slots.Add(slot);
            }
            return slot;
        }

        #region Application Events

        private HookSlot<EventHandler>? _beginCustomizationMode;
        public event EventHandler BeginCustomizationMode
        {
            add => Slot(
                ref _beginCustomizationMode,
                () => Application.BeginCustomizationMode += FwdBeginCustomizationMode,
                () => Application.BeginCustomizationMode -= FwdBeginCustomizationMode).Add(value);
            remove => _beginCustomizationMode?.Remove(value);
        }
        private void FwdBeginCustomizationMode(object? s, EventArgs e) => _beginCustomizationMode?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<BeginDoubleClickEventArgs>>? _beginDoubleClick;
        public event EventHandler<BeginDoubleClickEventArgs> BeginDoubleClick
        {
            add => Slot(
                ref _beginDoubleClick,
                () => Application.BeginDoubleClick += FwdBeginDoubleClick,
                () => Application.BeginDoubleClick -= FwdBeginDoubleClick).Add(value);
            remove => _beginDoubleClick?.Remove(value);
        }
        private void FwdBeginDoubleClick(object? s, BeginDoubleClickEventArgs e) => _beginDoubleClick?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler>? _beginQuit;
        public event EventHandler BeginQuit
        {
            add => Slot(
                ref _beginQuit,
                () => Application.BeginQuit += FwdBeginQuit,
                () => Application.BeginQuit -= FwdBeginQuit).Add(value);
            remove => _beginQuit?.Remove(value);
        }
        private void FwdBeginQuit(object? s, EventArgs e) => _beginQuit?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<TabbedDialogEventArgs>>? _displayingCustomizeDialog;
        public event EventHandler<TabbedDialogEventArgs> DisplayingCustomizeDialog
        {
            add => Slot(
                ref _displayingCustomizeDialog,
                () => Application.DisplayingCustomizeDialog += FwdDisplayingCustomizeDialog,
                () => Application.DisplayingCustomizeDialog -= FwdDisplayingCustomizeDialog).Add(value);
            remove => _displayingCustomizeDialog?.Remove(value);
        }
        private void FwdDisplayingCustomizeDialog(object? s, TabbedDialogEventArgs e) => _displayingCustomizeDialog?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<TabbedDialogEventArgs>>? _displayingDraftingSettingsDialog;
        public event EventHandler<TabbedDialogEventArgs> DisplayingDraftingSettingsDialog
        {
            add => Slot(
                ref _displayingDraftingSettingsDialog,
                () => Application.DisplayingDraftingSettingsDialog += FwdDisplayingDraftingSettingsDialog,
                () => Application.DisplayingDraftingSettingsDialog -= FwdDisplayingDraftingSettingsDialog).Add(value);
            remove => _displayingDraftingSettingsDialog?.Remove(value);
        }
        private void FwdDisplayingDraftingSettingsDialog(object? s, TabbedDialogEventArgs e) => _displayingDraftingSettingsDialog?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<TabbedDialogEventArgs>>? _displayingOptionDialog;
        public event EventHandler<TabbedDialogEventArgs> DisplayingOptionDialog
        {
            add => Slot(
                ref _displayingOptionDialog,
                () => Application.DisplayingOptionDialog += FwdDisplayingOptionDialog,
                () => Application.DisplayingOptionDialog -= FwdDisplayingOptionDialog).Add(value);
            remove => _displayingOptionDialog?.Remove(value);
        }
        private void FwdDisplayingOptionDialog(object? s, TabbedDialogEventArgs e) => _displayingOptionDialog?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler>? _endCustomizationMode;
        public event EventHandler EndCustomizationMode
        {
            add => Slot(
                ref _endCustomizationMode,
                () => Application.EndCustomizationMode += FwdEndCustomizationMode,
                () => Application.EndCustomizationMode -= FwdEndCustomizationMode).Add(value);
            remove => _endCustomizationMode?.Remove(value);
        }
        private void FwdEndCustomizationMode(object? s, EventArgs e) => _endCustomizationMode?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler>? _enterModal;
        public event EventHandler EnterModal
        {
            add => Slot(
                ref _enterModal,
                () => Application.EnterModal += FwdEnterModal,
                () => Application.EnterModal -= FwdEnterModal).Add(value);
            remove => _enterModal?.Remove(value);
        }
        private void FwdEnterModal(object? s, EventArgs e) => _enterModal?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler>? _idle;
        public event EventHandler Idle
        {
            add => Slot(
                ref _idle,
                () => Application.Idle += FwdIdle,
                () => Application.Idle -= FwdIdle).Add(value);
            remove => _idle?.Remove(value);
        }
        private void FwdIdle(object? s, EventArgs e) => _idle?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler>? _leaveModal;
        public event EventHandler LeaveModal
        {
            add => Slot(
                ref _leaveModal,
                () => Application.LeaveModal += FwdLeaveModal,
                () => Application.LeaveModal -= FwdLeaveModal).Add(value);
            remove => _leaveModal?.Remove(value);
        }
        private void FwdLeaveModal(object? s, EventArgs e) => _leaveModal?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<PreTranslateMessageEventArgs>>? _preTranslateMessage;
        public event EventHandler<PreTranslateMessageEventArgs> PreTranslateMessage
        {
            add => Slot(
                ref _preTranslateMessage,
                () => Application.PreTranslateMessage += FwdPreTranslateMessage,
                () => Application.PreTranslateMessage -= FwdPreTranslateMessage).Add(value);
            remove => _preTranslateMessage?.Remove(value);
        }
        private void FwdPreTranslateMessage(object? s, PreTranslateMessageEventArgs e) => _preTranslateMessage?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler>? _quitAborted;
        public event EventHandler QuitAborted
        {
            add => Slot(
                ref _quitAborted,
                () => Application.QuitAborted += FwdQuitAborted,
                () => Application.QuitAborted -= FwdQuitAborted).Add(value);
            remove => _quitAborted?.Remove(value);
        }
        private void FwdQuitAborted(object? s, EventArgs e) => _quitAborted?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler>? _quitWillStart;
        public event EventHandler QuitWillStart
        {
            add => Slot(
                ref _quitWillStart,
                () => Application.QuitWillStart += FwdQuitWillStart,
                () => Application.QuitWillStart -= FwdQuitWillStart).Add(value);
            remove => _quitWillStart?.Remove(value);
        }
        private void FwdQuitWillStart(object? s, EventArgs e) => _quitWillStart?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<SystemVariableChangedEventArgs>>? _systemVariableChanged;
        public event EventHandler<SystemVariableChangedEventArgs> SystemVariableChanged
        {
            add => Slot(
                ref _systemVariableChanged,
                () => Application.SystemVariableChanged += FwdSystemVariableChanged,
                () => Application.SystemVariableChanged -= FwdSystemVariableChanged).Add(value);
            remove => _systemVariableChanged?.Remove(value);
        }
        private void FwdSystemVariableChanged(object? s, SystemVariableChangedEventArgs e) => _systemVariableChanged?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<SystemVariableChangingEventArgs>>? _systemVariableChanging;
        public event EventHandler<SystemVariableChangingEventArgs> SystemVariableChanging
        {
            add => Slot(
                ref _systemVariableChanging,
                () => Application.SystemVariableChanging += FwdSystemVariableChanging,
                () => Application.SystemVariableChanging -= FwdSystemVariableChanging).Add(value);
            remove => _systemVariableChanging?.Remove(value);
        }
        private void FwdSystemVariableChanging(object? s, SystemVariableChangingEventArgs e) => _systemVariableChanging?.Handlers?.Invoke(s, e);

        #endregion

        #region DocumentCollection Events

        private HookSlot<EventHandler<DocumentCollectionEventArgs>>? _documentActivated;
        public event EventHandler<DocumentCollectionEventArgs> DocumentActivated
        {
            add => Slot(
                ref _documentActivated,
                () => Application.DocumentManager.DocumentActivated += FwdDocumentActivated,
                () => Application.DocumentManager.DocumentActivated -= FwdDocumentActivated).Add(value);
            remove => _documentActivated?.Remove(value);
        }
        private void FwdDocumentActivated(object? s, DocumentCollectionEventArgs e) => _documentActivated?.Handlers?.Invoke(s, e);

        private HookSlot<DocumentActivationChangedEventHandler>? _documentActivationChanged;
        public event DocumentActivationChangedEventHandler DocumentActivationChanged
        {
            add => Slot(
                ref _documentActivationChanged,
                () => Application.DocumentManager.DocumentActivationChanged += FwdDocumentActivationChanged,
                () => Application.DocumentManager.DocumentActivationChanged -= FwdDocumentActivationChanged).Add(value);
            remove => _documentActivationChanged?.Remove(value);
        }
        private void FwdDocumentActivationChanged(object? s, DocumentActivationChangedEventArgs e) => _documentActivationChanged?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<DocumentCollectionEventArgs>>? _documentBecameCurrent;
        public event EventHandler<DocumentCollectionEventArgs> DocumentBecameCurrent
        {
            add => Slot(
                ref _documentBecameCurrent,
                () => Application.DocumentManager.DocumentBecameCurrent += FwdDocumentBecameCurrent,
                () => Application.DocumentManager.DocumentBecameCurrent -= FwdDocumentBecameCurrent).Add(value);
            remove => _documentBecameCurrent?.Remove(value);
        }
        private void FwdDocumentBecameCurrent(object? s, DocumentCollectionEventArgs e) => _documentBecameCurrent?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<DocumentCollectionEventArgs>>? _documentCreated;
        public event EventHandler<DocumentCollectionEventArgs> DocumentCreated
        {
            add => Slot(
                ref _documentCreated,
                () => Application.DocumentManager.DocumentCreated += FwdDocumentCreated,
                () => Application.DocumentManager.DocumentCreated -= FwdDocumentCreated).Add(value);
            remove => _documentCreated?.Remove(value);
        }
        private void FwdDocumentCreated(object? s, DocumentCollectionEventArgs e) => _documentCreated?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<DocumentCollectionEventArgs>>? _documentCreateStarted;
        public event EventHandler<DocumentCollectionEventArgs> DocumentCreateStarted
        {
            add => Slot(
                ref _documentCreateStarted,
                () => Application.DocumentManager.DocumentCreateStarted += FwdDocumentCreateStarted,
                () => Application.DocumentManager.DocumentCreateStarted -= FwdDocumentCreateStarted).Add(value);
            remove => _documentCreateStarted?.Remove(value);
        }
        private void FwdDocumentCreateStarted(object? s, DocumentCollectionEventArgs e) => _documentCreateStarted?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<DocumentCollectionEventArgs>>? _documentCreationCanceled;
        public event EventHandler<DocumentCollectionEventArgs> DocumentCreationCanceled
        {
            add => Slot(
                ref _documentCreationCanceled,
                () => Application.DocumentManager.DocumentCreationCanceled += FwdDocumentCreationCanceled,
                () => Application.DocumentManager.DocumentCreationCanceled -= FwdDocumentCreationCanceled).Add(value);
            remove => _documentCreationCanceled?.Remove(value);
        }
        private void FwdDocumentCreationCanceled(object? s, DocumentCollectionEventArgs e) => _documentCreationCanceled?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<DocumentDestroyedEventArgs>>? _documentDestroyed;
        public event EventHandler<DocumentDestroyedEventArgs> DocumentDestroyed
        {
            add => Slot(
                ref _documentDestroyed,
                () => Application.DocumentManager.DocumentDestroyed += FwdDocumentDestroyed,
                () => Application.DocumentManager.DocumentDestroyed -= FwdDocumentDestroyed).Add(value);
            remove => _documentDestroyed?.Remove(value);
        }
        private void FwdDocumentDestroyed(object? s, DocumentDestroyedEventArgs e) => _documentDestroyed?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<DocumentLockModeChangedEventArgs>>? _documentLockModeChanged;
        public event EventHandler<DocumentLockModeChangedEventArgs> DocumentLockModeChanged
        {
            add => Slot(
                ref _documentLockModeChanged,
                () => Application.DocumentManager.DocumentLockModeChanged += FwdDocumentLockModeChanged,
                () => Application.DocumentManager.DocumentLockModeChanged -= FwdDocumentLockModeChanged).Add(value);
            remove => _documentLockModeChanged?.Remove(value);
        }
        private void FwdDocumentLockModeChanged(object? s, DocumentLockModeChangedEventArgs e) => _documentLockModeChanged?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<DocumentLockModeChangeVetoedEventArgs>>? _documentLockModeChangeVetoed;
        public event EventHandler<DocumentLockModeChangeVetoedEventArgs> DocumentLockModeChangeVetoed
        {
            add => Slot(
                ref _documentLockModeChangeVetoed,
                () => Application.DocumentManager.DocumentLockModeChangeVetoed += FwdDocumentLockModeChangeVetoed,
                () => Application.DocumentManager.DocumentLockModeChangeVetoed -= FwdDocumentLockModeChangeVetoed).Add(value);
            remove => _documentLockModeChangeVetoed?.Remove(value);
        }
        private void FwdDocumentLockModeChangeVetoed(object? s, DocumentLockModeChangeVetoedEventArgs e) => _documentLockModeChangeVetoed?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<DocumentLockModeWillChangeEventArgs>>? _documentLockModeWillChange;
        public event EventHandler<DocumentLockModeWillChangeEventArgs> DocumentLockModeWillChange
        {
            add => Slot(
                ref _documentLockModeWillChange,
                () => Application.DocumentManager.DocumentLockModeWillChange += FwdDocumentLockModeWillChange,
                () => Application.DocumentManager.DocumentLockModeWillChange -= FwdDocumentLockModeWillChange).Add(value);
            remove => _documentLockModeWillChange?.Remove(value);
        }
        private void FwdDocumentLockModeWillChange(object? s, DocumentLockModeWillChangeEventArgs e) => _documentLockModeWillChange?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<DocumentCollectionEventArgs>>? _documentToBeActivated;
        public event EventHandler<DocumentCollectionEventArgs> DocumentToBeActivated
        {
            add => Slot(
                ref _documentToBeActivated,
                () => Application.DocumentManager.DocumentToBeActivated += FwdDocumentToBeActivated,
                () => Application.DocumentManager.DocumentToBeActivated -= FwdDocumentToBeActivated).Add(value);
            remove => _documentToBeActivated?.Remove(value);
        }
        private void FwdDocumentToBeActivated(object? s, DocumentCollectionEventArgs e) => _documentToBeActivated?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<DocumentCollectionEventArgs>>? _documentToBeDeactivated;
        public event EventHandler<DocumentCollectionEventArgs> DocumentToBeDeactivated
        {
            add => Slot(
                ref _documentToBeDeactivated,
                () => Application.DocumentManager.DocumentToBeDeactivated += FwdDocumentToBeDeactivated,
                () => Application.DocumentManager.DocumentToBeDeactivated -= FwdDocumentToBeDeactivated).Add(value);
            remove => _documentToBeDeactivated?.Remove(value);
        }
        private void FwdDocumentToBeDeactivated(object? s, DocumentCollectionEventArgs e) => _documentToBeDeactivated?.Handlers?.Invoke(s, e);

        private HookSlot<EventHandler<DocumentCollectionEventArgs>>? _documentToBeDestroyed;
        public event EventHandler<DocumentCollectionEventArgs> DocumentToBeDestroyed
        {
            add => Slot(
                ref _documentToBeDestroyed,
                () => Application.DocumentManager.DocumentToBeDestroyed += FwdDocumentToBeDestroyed,
                () => Application.DocumentManager.DocumentToBeDestroyed -= FwdDocumentToBeDestroyed).Add(value);
            remove => _documentToBeDestroyed?.Remove(value);
        }
        private void FwdDocumentToBeDestroyed(object? s, DocumentCollectionEventArgs e) => _documentToBeDestroyed?.Handlers?.Invoke(s, e);

        #endregion

        #region Active-Document Database Events

        // Object change notifications live on the Database, not on Application/DocumentManager.
        // These aggregate events follow the active document automatically: subscribe once and you
        // receive the active drawing's append/modify/erase events, rebinding on document switch.
        private bool _dbHookInstalled;
        private DbServices.Database? _boundDb;

        private HookSlot<DbServices.ObjectEventHandler>? _activeObjectAppended;
        public event DbServices.ObjectEventHandler ActiveObjectAppended
        {
            add => Slot(ref _activeObjectAppended, EnsureDbHook, ReleaseDbHook).Add(value);
            remove => _activeObjectAppended?.Remove(value);
        }

        private HookSlot<DbServices.ObjectEventHandler>? _activeObjectModified;
        public event DbServices.ObjectEventHandler ActiveObjectModified
        {
            add => Slot(ref _activeObjectModified, EnsureDbHook, ReleaseDbHook).Add(value);
            remove => _activeObjectModified?.Remove(value);
        }

        private HookSlot<DbServices.ObjectErasedEventHandler>? _activeObjectErased;
        public event DbServices.ObjectErasedEventHandler ActiveObjectErased
        {
            add => Slot(ref _activeObjectErased, EnsureDbHook, ReleaseDbHook).Add(value);
            remove => _activeObjectErased?.Remove(value);
        }

        private void EnsureDbHook()
        {
            if (_dbHookInstalled) return;
            _dbHookInstalled = true;
            DocumentActivated += OnActiveDocChanged;
            DocumentToBeDeactivated += OnActiveDocDeactivated;
            DocumentToBeDestroyed += OnActiveDocToBeDestroyed;
            DocumentDestroyed += OnActiveDocDestroyed;
            BindDatabase(Application.DocumentManager.MdiActiveDocument?.Database);
        }

        private void ReleaseDbHook()
        {
            if (!_dbHookInstalled) return;
            _dbHookInstalled = false;
            DocumentActivated -= OnActiveDocChanged;
            DocumentToBeDeactivated -= OnActiveDocDeactivated;
            DocumentToBeDestroyed -= OnActiveDocToBeDestroyed;
            DocumentDestroyed -= OnActiveDocDestroyed;
            BindDatabase(null);
        }

        private void OnActiveDocChanged(object? s, DocumentCollectionEventArgs e)
            => BindDatabase(e.Document?.Database);

        private void OnActiveDocDeactivated(object? s, DocumentCollectionEventArgs e)
            => BindDatabase(null);

        /// <summary>
        /// A drawing can be closed while it is still the active document, in which case no
        /// deactivate precedes the destroy and nothing else would release the binding. Let go of
        /// the database here, while unsubscribing from it is still safe.
        /// </summary>
        private void OnActiveDocToBeDestroyed(object? s, DocumentCollectionEventArgs e)
        {
            DbServices.Database? dying = null;
            try
            {
                dying = e.Document?.Database;
            }
            catch (Exception ex)
            {
                // The document is already on its way out. Treat it as unidentifiable and let go;
                // OnActiveDocDestroyed rebinds to whatever is active once the dust settles.
                EventManagerTrace.Report(
                    "failed to read the database of a document being destroyed", ex);
            }

            if (dying == null || ReferenceEquals(dying, _boundDb)) BindDatabase(null);
        }

        /// <summary>
        /// The document is gone. Rebind to whatever is active now: null when the last drawing was
        /// closed, the next drawing otherwise. This is also the backstop that releases a dead
        /// database if a destroy ever arrives without either a preceding deactivate or a
        /// resolvable document.
        /// </summary>
        private void OnActiveDocDestroyed(object? s, DocumentDestroyedEventArgs e)
        {
            Document? active = null;
            try
            {
                active = Application.DocumentManager.MdiActiveDocument;
            }
            catch (Exception ex)
            {
                EventManagerTrace.Report(
                    "failed to read the active document after a document was destroyed", ex);
            }

            BindDatabase(active?.Database);
        }

        private void BindDatabase(DbServices.Database? db)
        {
            if (ReferenceEquals(_boundDb, db)) return;

            var previous = _boundDb;

            // Move the field off the old database BEFORE detaching from it. AutoCAD may already
            // have torn that one down, in which case the detach throws; staying bound to a dead
            // Database afterwards is worse than leaking three handler references on an object
            // that is going away regardless.
            _boundDb = db;

            if (previous != null)
            {
                try
                {
                    previous.ObjectAppended -= FwdObjectAppended;
                    previous.ObjectModified -= FwdObjectModified;
                    previous.ObjectErased -= FwdObjectErased;
                }
                catch (Exception ex)
                {
                    EventManagerTrace.Report(
                        "failed to detach from the previously bound database", ex);
                }
            }

            if (db != null)
            {
                db.ObjectAppended += FwdObjectAppended;
                db.ObjectModified += FwdObjectModified;
                db.ObjectErased += FwdObjectErased;
            }
        }

        private void FwdObjectAppended(object? s, DbServices.ObjectEventArgs e)
            => _activeObjectAppended?.Handlers?.Invoke(s, e);
        private void FwdObjectModified(object? s, DbServices.ObjectEventArgs e)
            => _activeObjectModified?.Handlers?.Invoke(s, e);
        private void FwdObjectErased(object? s, DbServices.ObjectErasedEventArgs e)
            => _activeObjectErased?.Handlers?.Invoke(s, e);

        #endregion

        private void OnDocToBeDestroyed(object? sender, DocumentCollectionEventArgs e)
        {
            CleanupDocument(e.Document);
        }

        private void CleanupDocument(Document doc)
        {
            if (!_subscriptions.TryGetValue(doc, out var list)) return;
            foreach (var unsub in list) unsub();
            _subscriptions.Remove(doc);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var doc in _subscriptions.Keys.ToList())
                CleanupDocument(doc);

            // Index loop: releasing one slot can touch another (the database hook unsubscribes
            // itself from DocumentActivated), and _slots must tolerate that.
            for (int i = 0; i < _slots.Count; i++) _slots[i].Release();
            _slots.Clear();

            Application.DocumentManager.DocumentToBeDestroyed -= OnDocToBeDestroyed;
        }
    }
}
