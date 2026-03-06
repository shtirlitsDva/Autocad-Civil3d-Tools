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

        public async Task<WaitResult> WaitUntilAsync(string stepName, Func<bool> condition, CancellationToken cancellationToken)
            => await WaitUntilAsync(stepName, condition, cancellationToken, continueOnCapturedContext: true);

        public async Task<WaitResult> WaitUntilAsync(
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
                    if (cancellationToken.IsCancellationRequested)
                        return WaitResult.Cancelled;

                    if (condition())
                        return WaitResult.Completed;

                    if (sw.Elapsed >= waitPolicy.Timeout)
                    {
                        throw new TimeoutException($"Timed out after {waitPolicy.Timeout.TotalSeconds:F0}s waiting for: {stepName}");
                    }

                    if (sw.Elapsed >= waitPolicy.OverlayThreshold)
                    {
                        overlayPresenter.Show(stepName, sw.Elapsed);
                    }

                    try
                    {
                        await Task.Delay(waitPolicy.PollInterval, cancellationToken)
                            .ConfigureAwait(continueOnCapturedContext);
                    }
                    catch (TaskCanceledException)
                    {
                        return WaitResult.Cancelled;
                    }
                }
            }
            finally
            {
                overlayPresenter.Hide();
            }
        }
    }
}
