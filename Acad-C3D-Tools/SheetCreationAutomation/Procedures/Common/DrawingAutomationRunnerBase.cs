using Autodesk.AutoCAD.ApplicationServices;
using SheetCreationAutomation.Models;
using SheetCreationAutomation.Services;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SheetCreationAutomation.Procedures.Common
{
    internal abstract class DrawingAutomationRunnerBase
    {
        protected DrawingAutomationRunnerBase(WaitPolicy waitPolicy, IWaitOverlayPresenter overlayPresenter)
        {
            WaitPolicy = waitPolicy;
            Waiter = new AutomationWaiter(waitPolicy, overlayPresenter);
        }

        protected WaitPolicy WaitPolicy { get; }
        protected AutomationWaiter Waiter { get; }

        protected async Task WaitForDocumentActiveAsync(Document doc, CancellationToken cancellationToken)
        {
            RequestDocumentActivation(doc);

            await Waiter.WaitUntilAsync($"Document active: {Path.GetFileName(doc.Name)}", () =>
            {
                if (AcApp.DocumentManager.MdiActiveDocument == doc)
                {
                    return true;
                }

                RequestDocumentActivation(doc);
                return false;
            }, cancellationToken);
        }

        protected static void RequestDocumentActivation(Document doc)
        {
            if (AcApp.DocumentManager.MdiActiveDocument == doc)
            {
                return;
            }

            AcApp.DocumentManager.MdiActiveDocument = doc;
        }

        protected async Task WaitForIdleAsync(CancellationToken cancellationToken)
        {
            await Waiter.WaitUntilAsync("AutoCAD command idle", () =>
            {
                object? cmdNames = AcApp.GetSystemVariable("CMDNAMES");
                string activeCommands = cmdNames?.ToString() ?? string.Empty;
                return string.IsNullOrWhiteSpace(activeCommands);
            }, cancellationToken);
        }
    }
}
