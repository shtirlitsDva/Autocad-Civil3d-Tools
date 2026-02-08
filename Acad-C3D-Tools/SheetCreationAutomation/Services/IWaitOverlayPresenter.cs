using System;

namespace SheetCreationAutomation.Services
{
    internal interface IWaitOverlayPresenter
    {
        void Show(string stepName, TimeSpan elapsed);
        void Hide();
    }
}
