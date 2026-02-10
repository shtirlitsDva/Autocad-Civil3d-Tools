using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using UiSnoop.Models;
using UiSnoop.Services;

namespace UiSnoop.ViewModels;

internal class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IUiSnoopCaptureService captureService;
    private readonly DispatcherTimer timer;
    private SnapshotData latestSnapshot = new();

    private bool followMouse = true;
    private bool suspendUpdates;
    private string intervalMillisecondsText = "200";
    private string statusText = string.Empty;
    private string humanOutput = string.Empty;
    private string llmOutput = string.Empty;
    private string jsonOutput = string.Empty;
    private string targetHandleText = string.Empty;
    private string inspectorTextPayload = string.Empty;
    private string inspectorOutput = string.Empty;
    private bool isCtrlFreezeActive;

    public MainWindowViewModel(IUiSnoopCaptureService captureService)
    {
        this.captureService = captureService;

        ApplyIntervalCommand = new RelayCommand(ApplyInterval);
        CaptureNowCommand = new RelayCommand(CaptureNow);
        CopyLlmBlockCommand = new RelayCommand(CopyLlmBlock);
        CopyJsonCommand = new RelayCommand(CopyJson);
        CopySnapshotCommand = new RelayCommand(CopySnapshot);
        UseControlAsTargetCommand = new RelayCommand(UseControlAsTarget);
        UseWindowAsTargetCommand = new RelayCommand(UseWindowAsTarget);
        UseActiveAsTargetCommand = new RelayCommand(UseActiveAsTarget);
        ProbeTargetCommand = new RelayCommand(ProbeTarget);
        InspectorFocusCommand = new RelayCommand(() => ExecuteInspectorAction(InspectorAction.Focus));
        InspectorSetTextCommand = new RelayCommand(() => ExecuteInspectorAction(InspectorAction.SetText));
        InspectorEnterSendMessageCommand = new RelayCommand(() => ExecuteInspectorAction(InspectorAction.SendEnterSendMessage));
        InspectorEnterPostMessageCommand = new RelayCommand(() => ExecuteInspectorAction(InspectorAction.SendEnterPostMessage));
        InspectorEnterSendInputCommand = new RelayCommand(() => ExecuteInspectorAction(InspectorAction.SendEnterSendInput));
        InspectorNotifyEnUpdateCommand = new RelayCommand(() => ExecuteInspectorAction(InspectorAction.NotifyEnUpdate));
        InspectorNotifyEnChangeCommand = new RelayCommand(() => ExecuteInspectorAction(InspectorAction.NotifyEnChange));
        InspectorNotifyEnKillFocusCommand = new RelayCommand(() => ExecuteInspectorAction(InspectorAction.NotifyEnKillFocus));
        InspectorSendIdOkCommand = new RelayCommand(() => ExecuteInspectorAction(InspectorAction.SendDialogIdOk));
        InspectorClickButtonCommand = new RelayCommand(() => ExecuteInspectorAction(InspectorAction.ClickButton));
        CopyInspectorLogCommand = new RelayCommand(CopyInspectorLog);

        timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        timer.Tick += TimerOnTick;
        timer.Start();

        CaptureAndRender();
    }

    public bool FollowMouse
    {
        get => followMouse;
        set
        {
            if (SetProperty(ref followMouse, value))
            {
                CaptureAndRender();
            }
        }
    }

    public bool SuspendUpdates
    {
        get => suspendUpdates;
        set
        {
            if (!SetProperty(ref suspendUpdates, value))
            {
                return;
            }

            StatusText = value ? "Updates suspended." : "Updates resumed.";
            if (!value)
            {
                CaptureAndRender();
            }
        }
    }

    public string IntervalMillisecondsText
    {
        get => intervalMillisecondsText;
        set => SetProperty(ref intervalMillisecondsText, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public string HumanOutput
    {
        get => humanOutput;
        private set => SetProperty(ref humanOutput, value);
    }

    public string LlmOutput
    {
        get => llmOutput;
        private set => SetProperty(ref llmOutput, value);
    }

    public string JsonOutput
    {
        get => jsonOutput;
        private set => SetProperty(ref jsonOutput, value);
    }

    public string TargetHandleText
    {
        get => targetHandleText;
        set => SetProperty(ref targetHandleText, value);
    }

    public string InspectorTextPayload
    {
        get => inspectorTextPayload;
        set => SetProperty(ref inspectorTextPayload, value);
    }

    public string InspectorOutput
    {
        get => inspectorOutput;
        private set => SetProperty(ref inspectorOutput, value);
    }

    public IRelayCommand ApplyIntervalCommand { get; }
    public IRelayCommand CaptureNowCommand { get; }
    public IRelayCommand CopyLlmBlockCommand { get; }
    public IRelayCommand CopyJsonCommand { get; }
    public IRelayCommand CopySnapshotCommand { get; }
    public IRelayCommand UseControlAsTargetCommand { get; }
    public IRelayCommand UseWindowAsTargetCommand { get; }
    public IRelayCommand UseActiveAsTargetCommand { get; }
    public IRelayCommand ProbeTargetCommand { get; }
    public IRelayCommand InspectorFocusCommand { get; }
    public IRelayCommand InspectorSetTextCommand { get; }
    public IRelayCommand InspectorEnterSendMessageCommand { get; }
    public IRelayCommand InspectorEnterPostMessageCommand { get; }
    public IRelayCommand InspectorEnterSendInputCommand { get; }
    public IRelayCommand InspectorNotifyEnUpdateCommand { get; }
    public IRelayCommand InspectorNotifyEnChangeCommand { get; }
    public IRelayCommand InspectorNotifyEnKillFocusCommand { get; }
    public IRelayCommand InspectorSendIdOkCommand { get; }
    public IRelayCommand InspectorClickButtonCommand { get; }
    public IRelayCommand CopyInspectorLogCommand { get; }

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= TimerOnTick;
    }

    private void TimerOnTick(object? sender, EventArgs e)
    {
        if (IsControlPressed())
        {
            if (!isCtrlFreezeActive)
            {
                isCtrlFreezeActive = true;
                StatusText = "Frozen while Ctrl is pressed.";
            }

            return;
        }

        if (isCtrlFreezeActive)
        {
            isCtrlFreezeActive = false;
            if (SuspendUpdates)
            {
                StatusText = "Ctrl released. Updates remain suspended.";
                return;
            }
        }

        if (SuspendUpdates)
        {
            return;
        }

        CaptureAndRender();
    }

    private void CaptureNow()
    {
        CaptureAndRender();
    }

    private void ApplyInterval()
    {
        if (!int.TryParse(IntervalMillisecondsText, out int ms) || ms < 50)
        {
            StatusText = "Invalid interval. Use integer >= 50.";
            return;
        }

        timer.Interval = TimeSpan.FromMilliseconds(ms);
        StatusText = $"Interval set to {ms} ms.";
    }

    private void CopyLlmBlock()
    {
        captureService.CopyToClipboard(LlmOutput);
        StatusText = "Copied LLM block.";
    }

    private void CopyJson()
    {
        captureService.CopyToClipboard(JsonOutput);
        StatusText = "Copied JSON.";
    }

    private void CopySnapshot()
    {
        captureService.CopyToClipboard(HumanOutput);
        StatusText = "Copied technical snapshot.";
    }

    private void UseControlAsTarget()
    {
        TargetHandleText = latestSnapshot.ControlUnderMouse.HandleHex;
        StatusText = $"Target = control under mouse ({TargetHandleText}).";
    }

    private void UseWindowAsTarget()
    {
        TargetHandleText = latestSnapshot.WindowUnderMouse.HandleHex;
        StatusText = $"Target = window under mouse ({TargetHandleText}).";
    }

    private void UseActiveAsTarget()
    {
        TargetHandleText = latestSnapshot.ActiveWindow.HandleHex;
        StatusText = $"Target = active window ({TargetHandleText}).";
    }

    private void ProbeTarget()
    {
        if (!TryParseHandle(TargetHandleText, out IntPtr hwnd, out string error))
        {
            StatusText = error;
            return;
        }

        WindowInfo info = captureService.InspectHandle(hwnd);
        AppendInspectorLog(
            "probe",
            true,
            $"handle={info.HandleHex} class='{info.ClassName}' title='{info.Title}' pid={info.ProcessId} process='{info.ProcessName}' enabled={info.IsEnabled} visible={info.IsVisible}");
        StatusText = $"Probed {info.HandleHex} ({info.ClassName}).";
    }

    private void ExecuteInspectorAction(InspectorAction action)
    {
        if (!TryParseHandle(TargetHandleText, out IntPtr hwnd, out string error))
        {
            StatusText = error;
            return;
        }

        InspectorActionResult result = captureService.ExecuteInspectorAction(hwnd, action, InspectorTextPayload);
        string actionName = action.ToString();
        AppendInspectorLog(actionName, result.Succeeded, result.Message);
        StatusText = result.Succeeded ? $"Inspector action OK: {actionName}." : $"Inspector action failed: {actionName}.";
    }

    private void CopyInspectorLog()
    {
        captureService.CopyToClipboard(InspectorOutput);
        StatusText = "Copied inspector log.";
    }

    private void CaptureAndRender()
    {
        SnoopRenderResult result = captureService.Capture(FollowMouse);

        latestSnapshot = result.Snapshot;
        HumanOutput = result.HumanOutput;
        LlmOutput = result.LlmOutput;
        JsonOutput = result.JsonOutput;
        StatusText = $"Captured {result.Snapshot.TimestampLocal}.";
    }

    private void AppendInspectorLog(string action, bool success, string message)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(InspectorOutput))
        {
            sb.AppendLine(InspectorOutput.TrimEnd());
            sb.AppendLine();
        }

        sb.AppendLine("event_probe:");
        sb.AppendLine($"  timestamp_local: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine($"  action: {action}");
        sb.AppendLine($"  target_handle: \"{TargetHandleText}\"");
        sb.AppendLine($"  success: {success}");
        sb.AppendLine($"  message: \"{EscapeYaml(message)}\"");

        InspectorOutput = sb.ToString();
    }

    private static bool TryParseHandle(string value, out IntPtr hwnd, out string error)
    {
        hwnd = IntPtr.Zero;
        error = string.Empty;

        string text = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Target handle is empty.";
            return false;
        }

        NumberStyles styles = NumberStyles.Integer;
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
            styles = NumberStyles.AllowHexSpecifier;
        }

        if (!long.TryParse(text, styles, CultureInfo.InvariantCulture, out long numeric) || numeric == 0)
        {
            error = "Invalid target handle. Use decimal or 0xHEX.";
            return false;
        }

        hwnd = new IntPtr(numeric);
        return true;
    }

    private static string EscapeYaml(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
    }

    private static bool IsControlPressed()
    {
        const short keyDownMask = unchecked((short)0x8000);
        return (GetAsyncKeyState(VkControl) & keyDownMask) != 0
               || (GetAsyncKeyState(VkLControl) & keyDownMask) != 0
               || (GetAsyncKeyState(VkRControl) & keyDownMask) != 0;
    }

    private const int VkControl = 0x11;
    private const int VkLControl = 0xA2;
    private const int VkRControl = 0xA3;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
