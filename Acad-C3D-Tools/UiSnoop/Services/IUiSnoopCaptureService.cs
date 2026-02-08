using UiSnoop.Models;

namespace UiSnoop.Services;

internal interface IUiSnoopCaptureService
{
    SnoopRenderResult Capture(bool followMouse);
    void CopyToClipboard(string text);
}