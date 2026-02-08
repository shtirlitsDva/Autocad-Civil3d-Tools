using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using UiSnoop.Models;
using UiSnoop.Services;

namespace UiSnoop.ViewModels;

internal class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IUiSnoopCaptureService captureService;
    private readonly DispatcherTimer timer;

    private bool followMouse = true;
    private bool suspendUpdates;
    private string intervalMillisecondsText = "200";
    private string statusText = string.Empty;
    private string humanOutput = string.Empty;
    private string llmOutput = string.Empty;
    private string jsonOutput = string.Empty;
    private bool isCtrlFreezeActive;

    public MainWindowViewModel(IUiSnoopCaptureService captureService)
    {
        this.captureService = captureService;

        ApplyIntervalCommand = new RelayCommand(ApplyInterval);
        CaptureNowCommand = new RelayCommand(CaptureNow);
        CopyLlmBlockCommand = new RelayCommand(CopyLlmBlock);
        CopyJsonCommand = new RelayCommand(CopyJson);
        CopySnapshotCommand = new RelayCommand(CopySnapshot);

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

    public IRelayCommand ApplyIntervalCommand { get; }
    public IRelayCommand CaptureNowCommand { get; }
    public IRelayCommand CopyLlmBlockCommand { get; }
    public IRelayCommand CopyJsonCommand { get; }
    public IRelayCommand CopySnapshotCommand { get; }

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

    private void CaptureAndRender()
    {
        SnoopRenderResult result = captureService.Capture(FollowMouse);

        HumanOutput = result.HumanOutput;
        LlmOutput = result.LlmOutput;
        JsonOutput = result.JsonOutput;
        StatusText = $"Captured {result.Snapshot.TimestampLocal}.";
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
