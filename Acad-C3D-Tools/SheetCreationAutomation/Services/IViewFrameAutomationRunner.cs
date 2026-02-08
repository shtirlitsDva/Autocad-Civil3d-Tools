using SheetCreationAutomation.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SheetCreationAutomation.Services
{
    internal interface IViewFrameAutomationRunner
    {
        Task<AutomationRunResult> RunAsync(
            ViewFrameAutomationContext context,
            IProgress<string> progress,
            CancellationToken cancellationToken);
    }
}
