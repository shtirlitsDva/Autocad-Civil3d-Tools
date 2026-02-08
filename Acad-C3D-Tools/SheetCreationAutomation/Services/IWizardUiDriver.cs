using SheetCreationAutomation.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SheetCreationAutomation.Services
{
    internal interface IWizardUiDriver
    {
        Task RunCreateViewFramesWizardAsync(
            WizardRunOptions options,
            IProgress<string> progress,
            CancellationToken cancellationToken);

        string LastSelectorSnapshot { get; }
    }
}
