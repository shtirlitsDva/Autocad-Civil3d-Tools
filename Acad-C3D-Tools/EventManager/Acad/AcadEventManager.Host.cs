#nullable enable

// The host namespaces AcadEventManager.cs names its types through. Global, so their names are ones no consumer
// uses for anything else.
global using EventHostApp = Autodesk.AutoCAD.ApplicationServices;
global using EventHostDb = Autodesk.AutoCAD.DatabaseServices;

using System;

using Autodesk.AutoCAD.ApplicationServices;

namespace EventManager
{
    /// <summary>
    /// AutoCAD's part of <see cref="AcadEventManager"/>: the idle source behind <see cref="RunOnNextIdle"/>, and the
    /// application events only AutoCAD raises. The BricsCAD part is <c>Brx\AcadEventManager.Host.cs</c>; the
    /// .projitems compiles one of the two by the consuming project's <c>NorsynHost</c> (AutoCAD when unset).
    /// </summary>
    public partial class AcadEventManager
    {
        // AutoCAD raises Idle reliably, so the queue rides this manager's own Idle event.
        private void ArmIdle() => Idle += OnIdleTick;

        private void DisarmIdle() => Idle -= OnIdleTick;

        private void OnIdleTick(object? sender, EventArgs e) => _idleDrain.Tick();

        #region Application events BricsCAD does not have

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

        #endregion
    }
}
