using System;

namespace SheetCreationAutomation.Services
{
    internal interface IWaitOverlayPresenter
    {
        Action? CancelAction { get; set; }
        void Show(string stepName, TimeSpan elapsed);
        void Hide();
    }
}
