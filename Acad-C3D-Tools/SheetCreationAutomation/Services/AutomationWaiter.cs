using SheetCreationAutomation.Models;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SheetCreationAutomation.Services
{
    internal sealed class AutomationWaiter
    {
        private readonly WaitPolicy waitPolicy;
        private readonly IWaitOverlayPresenter overlayPresenter;

        public AutomationWaiter(WaitPolicy waitPolicy, IWaitOverlayPresenter overlayPresenter)
        {
            this.waitPolicy = waitPolicy;
            this.overlayPresenter = overlayPresenter;
        }

        public async Task WaitUntilAsync(string stepName, Func<bool> condition, CancellationToken cancellationToken)
            => await WaitUntilAsync(stepName, condition, cancellationToken, continueOnCapturedContext: true);

        public async Task WaitUntilAsync(
            string stepName,
            Func<bool> condition,
            CancellationToken cancellationToken,
            bool continueOnCapturedContext)
        {
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (condition())
                    {
                        return;
                    }

                    if (sw.Elapsed >= waitPolicy.Timeout)
                    {
                        throw new TimeoutException($"Timed out after {waitPolicy.Timeout.TotalSeconds:F0}s waiting for: {stepName}");
                    }

                    if (sw.Elapsed >= waitPolicy.OverlayThreshold)
                    {
                        overlayPresenter.Show(stepName, sw.Elapsed);
                    }

                    await Task.Delay(waitPolicy.PollInterval, cancellationToken)
                        .ConfigureAwait(continueOnCapturedContext);
                }
            }
            finally
            {
                overlayPresenter.Hide();
            }
        }
    }
}
