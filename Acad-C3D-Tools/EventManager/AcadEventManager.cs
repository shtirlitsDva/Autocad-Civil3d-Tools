using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.AutoCAD.ApplicationServices;

namespace EventManager
{
    public class AcadEventManager : IDisposable
    {
        private readonly Dictionary<Document, List<Action>> _subscriptions = new();
        private readonly List<Action> _cleanup = new();
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

        #region Application Events

        private List<EventHandler> _beginCustomizationMode;
        public event EventHandler BeginCustomizationMode
        {
            add
            {
                if (_beginCustomizationMode == null)
                {
                    _beginCustomizationMode = new();
                    Application.BeginCustomizationMode += FwdBeginCustomizationMode;
                    _cleanup.Add(() => Application.BeginCustomizationMode -= FwdBeginCustomizationMode);
                }
                _beginCustomizationMode.Add(value);
            }
            remove { _beginCustomizationMode?.Remove(value); }
        }
        private void FwdBeginCustomizationMode(object s, EventArgs e)
        {
            if (_beginCustomizationMode == null) return;
            foreach (var h in _beginCustomizationMode) h(s, e);
        }

        private List<EventHandler<BeginDoubleClickEventArgs>> _beginDoubleClick;
        public event EventHandler<BeginDoubleClickEventArgs> BeginDoubleClick
        {
            add
            {
                if (_beginDoubleClick == null)
                {
                    _beginDoubleClick = new();
                    Application.BeginDoubleClick += FwdBeginDoubleClick;
                    _cleanup.Add(() => Application.BeginDoubleClick -= FwdBeginDoubleClick);
                }
                _beginDoubleClick.Add(value);
            }
            remove { _beginDoubleClick?.Remove(value); }
        }
        private void FwdBeginDoubleClick(object s, BeginDoubleClickEventArgs e)
        {
            if (_beginDoubleClick == null) return;
            foreach (var h in _beginDoubleClick) h(s, e);
        }

        private List<EventHandler> _beginQuit;
        public event EventHandler BeginQuit
        {
            add
            {
                if (_beginQuit == null)
                {
                    _beginQuit = new();
                    Application.BeginQuit += FwdBeginQuit;
                    _cleanup.Add(() => Application.BeginQuit -= FwdBeginQuit);
                }
                _beginQuit.Add(value);
            }
            remove { _beginQuit?.Remove(value); }
        }
        private void FwdBeginQuit(object s, EventArgs e)
        {
            if (_beginQuit == null) return;
            foreach (var h in _beginQuit) h(s, e);
        }

        private List<EventHandler<TabbedDialogEventArgs>> _displayingCustomizeDialog;
        public event EventHandler<TabbedDialogEventArgs> DisplayingCustomizeDialog
        {
            add
            {
                if (_displayingCustomizeDialog == null)
                {
                    _displayingCustomizeDialog = new();
                    Application.DisplayingCustomizeDialog += FwdDisplayingCustomizeDialog;
                    _cleanup.Add(() => Application.DisplayingCustomizeDialog -= FwdDisplayingCustomizeDialog);
                }
                _displayingCustomizeDialog.Add(value);
            }
            remove { _displayingCustomizeDialog?.Remove(value); }
        }
        private void FwdDisplayingCustomizeDialog(object s, TabbedDialogEventArgs e)
        {
            if (_displayingCustomizeDialog == null) return;
            foreach (var h in _displayingCustomizeDialog) h(s, e);
        }

        private List<EventHandler<TabbedDialogEventArgs>> _displayingDraftingSettingsDialog;
        public event EventHandler<TabbedDialogEventArgs> DisplayingDraftingSettingsDialog
        {
            add
            {
                if (_displayingDraftingSettingsDialog == null)
                {
                    _displayingDraftingSettingsDialog = new();
                    Application.DisplayingDraftingSettingsDialog += FwdDisplayingDraftingSettingsDialog;
                    _cleanup.Add(() => Application.DisplayingDraftingSettingsDialog -= FwdDisplayingDraftingSettingsDialog);
                }
                _displayingDraftingSettingsDialog.Add(value);
            }
            remove { _displayingDraftingSettingsDialog?.Remove(value); }
        }
        private void FwdDisplayingDraftingSettingsDialog(object s, TabbedDialogEventArgs e)
        {
            if (_displayingDraftingSettingsDialog == null) return;
            foreach (var h in _displayingDraftingSettingsDialog) h(s, e);
        }

        private List<EventHandler<TabbedDialogEventArgs>> _displayingOptionDialog;
        public event EventHandler<TabbedDialogEventArgs> DisplayingOptionDialog
        {
            add
            {
                if (_displayingOptionDialog == null)
                {
                    _displayingOptionDialog = new();
                    Application.DisplayingOptionDialog += FwdDisplayingOptionDialog;
                    _cleanup.Add(() => Application.DisplayingOptionDialog -= FwdDisplayingOptionDialog);
                }
                _displayingOptionDialog.Add(value);
            }
            remove { _displayingOptionDialog?.Remove(value); }
        }
        private void FwdDisplayingOptionDialog(object s, TabbedDialogEventArgs e)
        {
            if (_displayingOptionDialog == null) return;
            foreach (var h in _displayingOptionDialog) h(s, e);
        }

        private List<EventHandler> _endCustomizationMode;
        public event EventHandler EndCustomizationMode
        {
            add
            {
                if (_endCustomizationMode == null)
                {
                    _endCustomizationMode = new();
                    Application.EndCustomizationMode += FwdEndCustomizationMode;
                    _cleanup.Add(() => Application.EndCustomizationMode -= FwdEndCustomizationMode);
                }
                _endCustomizationMode.Add(value);
            }
            remove { _endCustomizationMode?.Remove(value); }
        }
        private void FwdEndCustomizationMode(object s, EventArgs e)
        {
            if (_endCustomizationMode == null) return;
            foreach (var h in _endCustomizationMode) h(s, e);
        }

        private List<EventHandler> _enterModal;
        public event EventHandler EnterModal
        {
            add
            {
                if (_enterModal == null)
                {
                    _enterModal = new();
                    Application.EnterModal += FwdEnterModal;
                    _cleanup.Add(() => Application.EnterModal -= FwdEnterModal);
                }
                _enterModal.Add(value);
            }
            remove { _enterModal?.Remove(value); }
        }
        private void FwdEnterModal(object s, EventArgs e)
        {
            if (_enterModal == null) return;
            foreach (var h in _enterModal) h(s, e);
        }

        private List<EventHandler> _idle;
        public event EventHandler Idle
        {
            add
            {
                if (_idle == null)
                {
                    _idle = new();
                    Application.Idle += FwdIdle;
                    _cleanup.Add(() => Application.Idle -= FwdIdle);
                }
                _idle.Add(value);
            }
            remove { _idle?.Remove(value); }
        }
        private void FwdIdle(object s, EventArgs e)
        {
            if (_idle == null) return;
            foreach (var h in _idle) h(s, e);
        }

        private List<EventHandler> _leaveModal;
        public event EventHandler LeaveModal
        {
            add
            {
                if (_leaveModal == null)
                {
                    _leaveModal = new();
                    Application.LeaveModal += FwdLeaveModal;
                    _cleanup.Add(() => Application.LeaveModal -= FwdLeaveModal);
                }
                _leaveModal.Add(value);
            }
            remove { _leaveModal?.Remove(value); }
        }
        private void FwdLeaveModal(object s, EventArgs e)
        {
            if (_leaveModal == null) return;
            foreach (var h in _leaveModal) h(s, e);
        }

        private List<EventHandler<PreTranslateMessageEventArgs>> _preTranslateMessage;
        public event EventHandler<PreTranslateMessageEventArgs> PreTranslateMessage
        {
            add
            {
                if (_preTranslateMessage == null)
                {
                    _preTranslateMessage = new();
                    Application.PreTranslateMessage += FwdPreTranslateMessage;
                    _cleanup.Add(() => Application.PreTranslateMessage -= FwdPreTranslateMessage);
                }
                _preTranslateMessage.Add(value);
            }
            remove { _preTranslateMessage?.Remove(value); }
        }
        private void FwdPreTranslateMessage(object s, PreTranslateMessageEventArgs e)
        {
            if (_preTranslateMessage == null) return;
            foreach (var h in _preTranslateMessage) h(s, e);
        }

        private List<EventHandler> _quitAborted;
        public event EventHandler QuitAborted
        {
            add
            {
                if (_quitAborted == null)
                {
                    _quitAborted = new();
                    Application.QuitAborted += FwdQuitAborted;
                    _cleanup.Add(() => Application.QuitAborted -= FwdQuitAborted);
                }
                _quitAborted.Add(value);
            }
            remove { _quitAborted?.Remove(value); }
        }
        private void FwdQuitAborted(object s, EventArgs e)
        {
            if (_quitAborted == null) return;
            foreach (var h in _quitAborted) h(s, e);
        }

        private List<EventHandler> _quitWillStart;
        public event EventHandler QuitWillStart
        {
            add
            {
                if (_quitWillStart == null)
                {
                    _quitWillStart = new();
                    Application.QuitWillStart += FwdQuitWillStart;
                    _cleanup.Add(() => Application.QuitWillStart -= FwdQuitWillStart);
                }
                _quitWillStart.Add(value);
            }
            remove { _quitWillStart?.Remove(value); }
        }
        private void FwdQuitWillStart(object s, EventArgs e)
        {
            if (_quitWillStart == null) return;
            foreach (var h in _quitWillStart) h(s, e);
        }

        private List<EventHandler<SystemVariableChangedEventArgs>> _systemVariableChanged;
        public event EventHandler<SystemVariableChangedEventArgs> SystemVariableChanged
        {
            add
            {
                if (_systemVariableChanged == null)
                {
                    _systemVariableChanged = new();
                    Application.SystemVariableChanged += FwdSystemVariableChanged;
                    _cleanup.Add(() => Application.SystemVariableChanged -= FwdSystemVariableChanged);
                }
                _systemVariableChanged.Add(value);
            }
            remove { _systemVariableChanged?.Remove(value); }
        }
        private void FwdSystemVariableChanged(object s, SystemVariableChangedEventArgs e)
        {
            if (_systemVariableChanged == null) return;
            foreach (var h in _systemVariableChanged) h(s, e);
        }

        private List<EventHandler<SystemVariableChangingEventArgs>> _systemVariableChanging;
        public event EventHandler<SystemVariableChangingEventArgs> SystemVariableChanging
        {
            add
            {
                if (_systemVariableChanging == null)
                {
                    _systemVariableChanging = new();
                    Application.SystemVariableChanging += FwdSystemVariableChanging;
                    _cleanup.Add(() => Application.SystemVariableChanging -= FwdSystemVariableChanging);
                }
                _systemVariableChanging.Add(value);
            }
            remove { _systemVariableChanging?.Remove(value); }
        }
        private void FwdSystemVariableChanging(object s, SystemVariableChangingEventArgs e)
        {
            if (_systemVariableChanging == null) return;
            foreach (var h in _systemVariableChanging) h(s, e);
        }

        #endregion

        #region DocumentCollection Events

        private List<EventHandler<DocumentCollectionEventArgs>> _documentActivated;
        public event EventHandler<DocumentCollectionEventArgs> DocumentActivated
        {
            add
            {
                if (_documentActivated == null)
                {
                    _documentActivated = new();
                    Application.DocumentManager.DocumentActivated += FwdDocumentActivated;
                    _cleanup.Add(() => Application.DocumentManager.DocumentActivated -= FwdDocumentActivated);
                }
                _documentActivated.Add(value);
            }
            remove { _documentActivated?.Remove(value); }
        }
        private void FwdDocumentActivated(object s, DocumentCollectionEventArgs e)
        {
            if (_documentActivated == null) return;
            foreach (var h in _documentActivated) h(s, e);
        }

        private List<DocumentActivationChangedEventHandler> _documentActivationChanged;
        public event DocumentActivationChangedEventHandler DocumentActivationChanged
        {
            add
            {
                if (_documentActivationChanged == null)
                {
                    _documentActivationChanged = new();
                    Application.DocumentManager.DocumentActivationChanged += FwdDocumentActivationChanged;
                    _cleanup.Add(() => Application.DocumentManager.DocumentActivationChanged -= FwdDocumentActivationChanged);
                }
                _documentActivationChanged.Add(value);
            }
            remove { _documentActivationChanged?.Remove(value); }
        }
        private void FwdDocumentActivationChanged(object s, DocumentActivationChangedEventArgs e)
        {
            if (_documentActivationChanged == null) return;
            foreach (var h in _documentActivationChanged) h(s, e);
        }

        private List<EventHandler<DocumentCollectionEventArgs>> _documentBecameCurrent;
        public event EventHandler<DocumentCollectionEventArgs> DocumentBecameCurrent
        {
            add
            {
                if (_documentBecameCurrent == null)
                {
                    _documentBecameCurrent = new();
                    Application.DocumentManager.DocumentBecameCurrent += FwdDocumentBecameCurrent;
                    _cleanup.Add(() => Application.DocumentManager.DocumentBecameCurrent -= FwdDocumentBecameCurrent);
                }
                _documentBecameCurrent.Add(value);
            }
            remove { _documentBecameCurrent?.Remove(value); }
        }
        private void FwdDocumentBecameCurrent(object s, DocumentCollectionEventArgs e)
        {
            if (_documentBecameCurrent == null) return;
            foreach (var h in _documentBecameCurrent) h(s, e);
        }

        private List<EventHandler<DocumentCollectionEventArgs>> _documentCreated;
        public event EventHandler<DocumentCollectionEventArgs> DocumentCreated
        {
            add
            {
                if (_documentCreated == null)
                {
                    _documentCreated = new();
                    Application.DocumentManager.DocumentCreated += FwdDocumentCreated;
                    _cleanup.Add(() => Application.DocumentManager.DocumentCreated -= FwdDocumentCreated);
                }
                _documentCreated.Add(value);
            }
            remove { _documentCreated?.Remove(value); }
        }
        private void FwdDocumentCreated(object s, DocumentCollectionEventArgs e)
        {
            if (_documentCreated == null) return;
            foreach (var h in _documentCreated) h(s, e);
        }

        private List<EventHandler<DocumentCollectionEventArgs>> _documentCreateStarted;
        public event EventHandler<DocumentCollectionEventArgs> DocumentCreateStarted
        {
            add
            {
                if (_documentCreateStarted == null)
                {
                    _documentCreateStarted = new();
                    Application.DocumentManager.DocumentCreateStarted += FwdDocumentCreateStarted;
                    _cleanup.Add(() => Application.DocumentManager.DocumentCreateStarted -= FwdDocumentCreateStarted);
                }
                _documentCreateStarted.Add(value);
            }
            remove { _documentCreateStarted?.Remove(value); }
        }
        private void FwdDocumentCreateStarted(object s, DocumentCollectionEventArgs e)
        {
            if (_documentCreateStarted == null) return;
            foreach (var h in _documentCreateStarted) h(s, e);
        }

        private List<EventHandler<DocumentCollectionEventArgs>> _documentCreationCanceled;
        public event EventHandler<DocumentCollectionEventArgs> DocumentCreationCanceled
        {
            add
            {
                if (_documentCreationCanceled == null)
                {
                    _documentCreationCanceled = new();
                    Application.DocumentManager.DocumentCreationCanceled += FwdDocumentCreationCanceled;
                    _cleanup.Add(() => Application.DocumentManager.DocumentCreationCanceled -= FwdDocumentCreationCanceled);
                }
                _documentCreationCanceled.Add(value);
            }
            remove { _documentCreationCanceled?.Remove(value); }
        }
        private void FwdDocumentCreationCanceled(object s, DocumentCollectionEventArgs e)
        {
            if (_documentCreationCanceled == null) return;
            foreach (var h in _documentCreationCanceled) h(s, e);
        }

        private List<EventHandler<DocumentDestroyedEventArgs>> _documentDestroyed;
        public event EventHandler<DocumentDestroyedEventArgs> DocumentDestroyed
        {
            add
            {
                if (_documentDestroyed == null)
                {
                    _documentDestroyed = new();
                    Application.DocumentManager.DocumentDestroyed += FwdDocumentDestroyed;
                    _cleanup.Add(() => Application.DocumentManager.DocumentDestroyed -= FwdDocumentDestroyed);
                }
                _documentDestroyed.Add(value);
            }
            remove { _documentDestroyed?.Remove(value); }
        }
        private void FwdDocumentDestroyed(object s, DocumentDestroyedEventArgs e)
        {
            if (_documentDestroyed == null) return;
            foreach (var h in _documentDestroyed) h(s, e);
        }

        private List<EventHandler<DocumentLockModeChangedEventArgs>> _documentLockModeChanged;
        public event EventHandler<DocumentLockModeChangedEventArgs> DocumentLockModeChanged
        {
            add
            {
                if (_documentLockModeChanged == null)
                {
                    _documentLockModeChanged = new();
                    Application.DocumentManager.DocumentLockModeChanged += FwdDocumentLockModeChanged;
                    _cleanup.Add(() => Application.DocumentManager.DocumentLockModeChanged -= FwdDocumentLockModeChanged);
                }
                _documentLockModeChanged.Add(value);
            }
            remove { _documentLockModeChanged?.Remove(value); }
        }
        private void FwdDocumentLockModeChanged(object s, DocumentLockModeChangedEventArgs e)
        {
            if (_documentLockModeChanged == null) return;
            foreach (var h in _documentLockModeChanged) h(s, e);
        }

        private List<EventHandler<DocumentLockModeChangeVetoedEventArgs>> _documentLockModeChangeVetoed;
        public event EventHandler<DocumentLockModeChangeVetoedEventArgs> DocumentLockModeChangeVetoed
        {
            add
            {
                if (_documentLockModeChangeVetoed == null)
                {
                    _documentLockModeChangeVetoed = new();
                    Application.DocumentManager.DocumentLockModeChangeVetoed += FwdDocumentLockModeChangeVetoed;
                    _cleanup.Add(() => Application.DocumentManager.DocumentLockModeChangeVetoed -= FwdDocumentLockModeChangeVetoed);
                }
                _documentLockModeChangeVetoed.Add(value);
            }
            remove { _documentLockModeChangeVetoed?.Remove(value); }
        }
        private void FwdDocumentLockModeChangeVetoed(object s, DocumentLockModeChangeVetoedEventArgs e)
        {
            if (_documentLockModeChangeVetoed == null) return;
            foreach (var h in _documentLockModeChangeVetoed) h(s, e);
        }

        private List<EventHandler<DocumentLockModeWillChangeEventArgs>> _documentLockModeWillChange;
        public event EventHandler<DocumentLockModeWillChangeEventArgs> DocumentLockModeWillChange
        {
            add
            {
                if (_documentLockModeWillChange == null)
                {
                    _documentLockModeWillChange = new();
                    Application.DocumentManager.DocumentLockModeWillChange += FwdDocumentLockModeWillChange;
                    _cleanup.Add(() => Application.DocumentManager.DocumentLockModeWillChange -= FwdDocumentLockModeWillChange);
                }
                _documentLockModeWillChange.Add(value);
            }
            remove { _documentLockModeWillChange?.Remove(value); }
        }
        private void FwdDocumentLockModeWillChange(object s, DocumentLockModeWillChangeEventArgs e)
        {
            if (_documentLockModeWillChange == null) return;
            foreach (var h in _documentLockModeWillChange) h(s, e);
        }

        private List<EventHandler<DocumentCollectionEventArgs>> _documentToBeActivated;
        public event EventHandler<DocumentCollectionEventArgs> DocumentToBeActivated
        {
            add
            {
                if (_documentToBeActivated == null)
                {
                    _documentToBeActivated = new();
                    Application.DocumentManager.DocumentToBeActivated += FwdDocumentToBeActivated;
                    _cleanup.Add(() => Application.DocumentManager.DocumentToBeActivated -= FwdDocumentToBeActivated);
                }
                _documentToBeActivated.Add(value);
            }
            remove { _documentToBeActivated?.Remove(value); }
        }
        private void FwdDocumentToBeActivated(object s, DocumentCollectionEventArgs e)
        {
            if (_documentToBeActivated == null) return;
            foreach (var h in _documentToBeActivated) h(s, e);
        }

        private List<EventHandler<DocumentCollectionEventArgs>> _documentToBeDeactivated;
        public event EventHandler<DocumentCollectionEventArgs> DocumentToBeDeactivated
        {
            add
            {
                if (_documentToBeDeactivated == null)
                {
                    _documentToBeDeactivated = new();
                    Application.DocumentManager.DocumentToBeDeactivated += FwdDocumentToBeDeactivated;
                    _cleanup.Add(() => Application.DocumentManager.DocumentToBeDeactivated -= FwdDocumentToBeDeactivated);
                }
                _documentToBeDeactivated.Add(value);
            }
            remove { _documentToBeDeactivated?.Remove(value); }
        }
        private void FwdDocumentToBeDeactivated(object s, DocumentCollectionEventArgs e)
        {
            if (_documentToBeDeactivated == null) return;
            foreach (var h in _documentToBeDeactivated) h(s, e);
        }

        private List<EventHandler<DocumentCollectionEventArgs>> _documentToBeDestroyed;
        public event EventHandler<DocumentCollectionEventArgs> DocumentToBeDestroyed
        {
            add
            {
                if (_documentToBeDestroyed == null)
                {
                    _documentToBeDestroyed = new();
                    Application.DocumentManager.DocumentToBeDestroyed += FwdDocumentToBeDestroyed;
                    _cleanup.Add(() => Application.DocumentManager.DocumentToBeDestroyed -= FwdDocumentToBeDestroyed);
                }
                _documentToBeDestroyed.Add(value);
            }
            remove { _documentToBeDestroyed?.Remove(value); }
        }
        private void FwdDocumentToBeDestroyed(object s, DocumentCollectionEventArgs e)
        {
            if (_documentToBeDestroyed == null) return;
            foreach (var h in _documentToBeDestroyed) h(s, e);
        }

        #endregion

        private void OnDocToBeDestroyed(object sender, DocumentCollectionEventArgs e)
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
            foreach (var unsub in _cleanup) unsub();
            _cleanup.Clear();
            Application.DocumentManager.DocumentToBeDestroyed -= OnDocToBeDestroyed;
        }
    }
}
